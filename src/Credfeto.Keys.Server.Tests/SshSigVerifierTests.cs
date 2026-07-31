using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Credfeto.Keys.Server.Crypto;
using FunFair.Test.Common;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class SshSigVerifierTests : TestBase
{
    private const string Namespace = "ssh-key-server-v1";
    private const string Challenge = "genuine-challenge";
    private const string HashAlgo = "sha256";

    [Fact]
    public void VerifyReturnsInvalidFormatForMalformedBase64KeyDataInsteadOfThrowing()
    {
        string pem = BuildSshSigPem(keyType: "ssh-ed25519");

        SshSigVerificationResult result = SshSigVerifier.Verify(
            sshSigPem: pem,
            challenge: "challenge",
            expectedKeyType: "ssh-ed25519",
            expectedKeyDataBase64: "AAAAA",
            expectedNamespace: Namespace
        );

        Assert.Equal(expected: SshSigVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void VerifyReturnsValidForGenuineEd25519Signature()
    {
        (byte[] pubKeyBlob, byte[] rawSig, string expectedKeyDataBase64) = CreateGenuineEd25519Signature();
        byte[] innerSigBytes = Concat(WireString("ssh-ed25519"), WireBytes(rawSig));
        string pem = BuildSshSigPem(pubKeyBlob: pubKeyBlob, hashAlgo: HashAlgo, innerSigBytes: innerSigBytes);

        SshSigVerificationResult result = SshSigVerifier.Verify(
            sshSigPem: pem,
            challenge: Challenge,
            expectedKeyType: "ssh-ed25519",
            expectedKeyDataBase64: expectedKeyDataBase64,
            expectedNamespace: Namespace
        );

        Assert.Equal(expected: SshSigVerificationResult.Valid, actual: result);
    }

    [Fact]
    public void VerifyReturnsInvalidSignatureForTamperedSignature()
    {
        (byte[] pubKeyBlob, byte[] rawSig, string expectedKeyDataBase64) = CreateGenuineEd25519Signature();
        rawSig[0] ^= 0xFF; // tamper with the signature
        byte[] innerSigBytes = Concat(WireString("ssh-ed25519"), WireBytes(rawSig));
        string pem = BuildSshSigPem(pubKeyBlob: pubKeyBlob, hashAlgo: HashAlgo, innerSigBytes: innerSigBytes);

        SshSigVerificationResult result = SshSigVerifier.Verify(
            sshSigPem: pem,
            challenge: Challenge,
            expectedKeyType: "ssh-ed25519",
            expectedKeyDataBase64: expectedKeyDataBase64,
            expectedNamespace: Namespace
        );

        Assert.Equal(expected: SshSigVerificationResult.InvalidSignature, actual: result);
    }

    [Fact]
    public void VerifyReturnsValidForGenuineSkEd25519Signature()
    {
        const string application = "ssh:";
        const byte flags = 0x01;
        const uint counter = 42;

        (byte[] publicKey, Ed25519PrivateKeyParameters privateKey) = GenerateEd25519KeyPair();
        byte[] pubKeyBlob = Concat(
            WireString("sk-ssh-ed25519@openssh.com"),
            WireBytes(publicKey),
            WireString(application)
        );

        byte[] sighashbuf = BuildSigHashBuf(hashAlgo: HashAlgo, challenge: Challenge);
        byte[] appHash = SHA256.HashData(Encoding.UTF8.GetBytes(application));
        byte[] clientDataHash = SHA256.HashData(sighashbuf);
        byte[] authData = Concat(appHash, [flags], WireUInt32(counter), clientDataHash);
        byte[] rawSig = SignEd25519(privateKey: privateKey, message: authData);

        byte[] innerSigBytes = Concat(
            WireString("sk-ssh-ed25519@openssh.com"),
            WireBytes(rawSig),
            [flags],
            WireUInt32(counter)
        );

        string pem = BuildSshSigPem(pubKeyBlob: pubKeyBlob, hashAlgo: HashAlgo, innerSigBytes: innerSigBytes);
        string expectedKeyDataBase64 = Convert.ToBase64String(pubKeyBlob);

        SshSigVerificationResult result = SshSigVerifier.Verify(
            sshSigPem: pem,
            challenge: Challenge,
            expectedKeyType: "sk-ssh-ed25519@openssh.com",
            expectedKeyDataBase64: expectedKeyDataBase64,
            expectedNamespace: Namespace
        );

        Assert.Equal(expected: SshSigVerificationResult.Valid, actual: result);
    }

    private static (byte[] PubKeyBlob, byte[] RawSignature, string ExpectedKeyDataBase64) CreateGenuineEd25519Signature()
    {
        (byte[] publicKey, Ed25519PrivateKeyParameters privateKey) = GenerateEd25519KeyPair();
        byte[] pubKeyBlob = Concat(WireString("ssh-ed25519"), WireBytes(publicKey));
        byte[] sighashbuf = BuildSigHashBuf(hashAlgo: HashAlgo, challenge: Challenge);
        byte[] rawSig = SignEd25519(privateKey: privateKey, message: sighashbuf);

        return (pubKeyBlob, rawSig, Convert.ToBase64String(pubKeyBlob));
    }

    private static (byte[] PublicKey, Ed25519PrivateKeyParameters PrivateKey) GenerateEd25519KeyPair()
    {
        Ed25519KeyPairGenerator generator = new();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

        AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
        Ed25519PrivateKeyParameters privateKey = (Ed25519PrivateKeyParameters)pair.Private;
        Ed25519PublicKeyParameters publicKey = (Ed25519PublicKeyParameters)pair.Public;

        return (publicKey.GetEncoded(), privateKey);
    }

    private static byte[] SignEd25519(Ed25519PrivateKeyParameters privateKey, byte[] message)
    {
        Ed25519Signer signer = new();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(message, off: 0, len: message.Length);

        return signer.GenerateSignature();
    }

    private static byte[] BuildSigHashBuf(string hashAlgo, string challenge)
    {
        byte[] challengeBytes = Encoding.UTF8.GetBytes(challenge);
        byte[] hash = hashAlgo switch
        {
            "sha256" => SHA256.HashData(challengeBytes),
            "sha512" => SHA512.HashData(challengeBytes),
            _ => throw new ArgumentOutOfRangeException(nameof(hashAlgo), hashAlgo, "Unsupported hash algorithm"),
        };

        // Matches production's SshSigVerifier.BuildSigHashBuf: per OpenSSH PROTOCOL.sshsig, the
        // signed blob has no uint32 SIG_VERSION field, unlike the outer envelope below.
        return Concat("SSHSIG"u8.ToArray(), WireString(Namespace), WireBytes([]), WireString(hashAlgo), WireBytes(hash));
    }

    private static string BuildSshSigPem(byte[] pubKeyBlob, string hashAlgo, byte[] innerSigBytes)
    {
        byte[] body = Concat(
            "SSHSIG"u8.ToArray(),
            WireUInt32(1),
            WireBytes(pubKeyBlob),
            WireString(Namespace),
            WireBytes([]),
            WireString(hashAlgo),
            WireBytes(innerSigBytes)
        );

        string base64 = Convert.ToBase64String(body);

        return $"-----BEGIN SSH SIGNATURE-----\n{base64}\n-----END SSH SIGNATURE-----";
    }

    private static string BuildSshSigPem(string keyType)
    {
        byte[] pubKeyBlob = Concat(WireString(keyType), WireBytes(new byte[32]));

        return BuildSshSigPem(pubKeyBlob: pubKeyBlob, hashAlgo: "sha256", innerSigBytes: []);
    }

    private static byte[] WireUInt32(uint value)
    {
        byte[] buffer = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

        return buffer;
    }

    private static byte[] WireBytes(byte[] value)
    {
        return Concat(WireUInt32((uint)value.Length), value);
    }

    private static byte[] WireString(string value)
    {
        return WireBytes(Encoding.UTF8.GetBytes(value));
    }

    private static byte[] Concat(params IReadOnlyList<byte[]> parts)
    {
        int length = 0;

        foreach (byte[] part in parts)
        {
            length += part.Length;
        }

        byte[] result = new byte[length];
        int offset = 0;

        foreach (byte[] part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
