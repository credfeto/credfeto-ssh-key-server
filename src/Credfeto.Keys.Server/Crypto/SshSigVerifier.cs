using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Credfeto.Keys.Server.Crypto;

public static class SshSigVerifier
{
    private const string SshSigBegin = "-----BEGIN SSH SIGNATURE-----";
    private const string SshSigEnd = "-----END SSH SIGNATURE-----";
    private const uint SupportedVersion = 1;

    // Ed25519 SubjectPublicKeyInfo DER prefix (12 bytes):
    // SEQUENCE { AlgorithmIdentifier { OID 1.3.101.112 } BIT STRING(0 unused bits) }
    private static ReadOnlySpan<byte> Ed25519SpkiPrefix =>
        [0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70, 0x03, 0x21, 0x00];

    public static SshSigVerificationResult Verify(
        string sshSigPem,
        string challenge,
        string expectedKeyType,
        string expectedKeyDataBase64,
        string expectedNamespace
    )
    {
        byte[] sigBytes;

        try
        {
            sigBytes = ParsePem(sshSigPem);
        }
        catch (FormatException)
        {
            return SshSigVerificationResult.InvalidFormat;
        }

        try
        {
            return VerifyCore(
                sigBytes: sigBytes,
                challenge: challenge,
                expectedKeyType: expectedKeyType,
                expectedKeyDataBase64: expectedKeyDataBase64,
                expectedNamespace: expectedNamespace
            );
        }
        catch (InvalidDataException)
        {
            return SshSigVerificationResult.InvalidFormat;
        }
    }

    private static SshSigVerificationResult VerifyCore(
        byte[] sigBytes,
        string challenge,
        string expectedKeyType,
        string expectedKeyDataBase64,
        string expectedNamespace
    )
    {
        ReadOnlySpan<byte> data = sigBytes;

        // Verify magic: "SSHSIG" (6 literal bytes, not length-prefixed)
        if (data.Length < 6 || !data[..6].SequenceEqual("SSHSIG"u8))
        {
            return SshSigVerificationResult.InvalidMagic;
        }

        int pos = 6;

        uint version = SshWireReader.ReadUInt32(data, ref pos);

        if (version != SupportedVersion)
        {
            return SshSigVerificationResult.UnsupportedVersion;
        }

        byte[] pubKeyBytes = SshWireReader.ReadStringBytes(data, ref pos);
        string ns = SshWireReader.ReadUtf8String(data, ref pos);
        _ = SshWireReader.ReadStringBytes(data, ref pos); // reserved
        string hashAlgo = SshWireReader.ReadUtf8String(data, ref pos);
        byte[] innerSigBytes = SshWireReader.ReadStringBytes(data, ref pos);

        if (!string.Equals(a: ns, b: expectedNamespace, comparisonType: StringComparison.Ordinal))
        {
            return SshSigVerificationResult.NamespaceMismatch;
        }

        if (
            !string.Equals(a: hashAlgo, b: "sha256", comparisonType: StringComparison.Ordinal)
            && !string.Equals(a: hashAlgo, b: "sha512", comparisonType: StringComparison.Ordinal)
        )
        {
            return SshSigVerificationResult.UnsupportedHashAlgorithm;
        }

        byte[] challengeBytes = Encoding.UTF8.GetBytes(challenge);
        byte[] sighashbuf = BuildSigHashBuf(ns: ns, hashAlgo: hashAlgo, messageBytes: challengeBytes);

        return VerifyKeyAndSignature(
            pubKeyBytes: pubKeyBytes,
            innerSigBytes: innerSigBytes,
            sighashbuf: sighashbuf,
            expectedKeyType: expectedKeyType,
            expectedKeyDataBase64: expectedKeyDataBase64
        );
    }

    private static SshSigVerificationResult VerifyKeyAndSignature(
        byte[] pubKeyBytes,
        byte[] innerSigBytes,
        byte[] sighashbuf,
        string expectedKeyType,
        string expectedKeyDataBase64
    )
    {
        int pkPos = 0;
        ReadOnlySpan<byte> pkSpan = pubKeyBytes;
        string sigKeyType = SshWireReader.ReadUtf8String(pkSpan, ref pkPos);

        if (!string.Equals(a: sigKeyType, b: expectedKeyType, comparisonType: StringComparison.Ordinal))
        {
            return SshSigVerificationResult.PublicKeyMismatch;
        }

        byte[] sigKeyBytes = SshWireReader.ReadStringBytes(pkSpan, ref pkPos);

        if (!Base64KeyData.TryDecode(expectedKeyDataBase64, bytes: out byte[]? expectedKeyBytes))
        {
            return SshSigVerificationResult.InvalidFormat;
        }

        if (!sigKeyBytes.AsSpan().SequenceEqual(expectedKeyBytes))
        {
            return SshSigVerificationResult.PublicKeyMismatch;
        }

        return sigKeyType switch
        {
            "ssh-ed25519" => VerifyEd25519Sig(
                innerSigBytes: innerSigBytes,
                sighashbuf: sighashbuf,
                keyBytes: sigKeyBytes
            ),
            "sk-ssh-ed25519@openssh.com" => VerifySkEd25519Sig(
                innerSigBytes: innerSigBytes,
                sighashbuf: sighashbuf,
                keyBytes: sigKeyBytes,
                pkSpan: pkSpan,
                pkPos: pkPos
            ),
            _ => SshSigVerificationResult.UnsupportedKeyType,
        };
    }

    private static SshSigVerificationResult VerifyEd25519Sig(byte[] innerSigBytes, byte[] sighashbuf, byte[] keyBytes)
    {
        int pos = 0;
        ReadOnlySpan<byte> sigSpan = innerSigBytes;
        string sigType = SshWireReader.ReadUtf8String(sigSpan, ref pos);

        if (!string.Equals(a: sigType, b: "ssh-ed25519", comparisonType: StringComparison.Ordinal))
        {
            return SshSigVerificationResult.InvalidFormat;
        }

        byte[] rawSig = SshWireReader.ReadStringBytes(sigSpan, ref pos);

        if (rawSig.Length != 64)
        {
            return SshSigVerificationResult.InvalidFormat;
        }

        return VerifyEd25519(publicKey32: keyBytes, message: sighashbuf, signature64: rawSig)
            ? SshSigVerificationResult.Valid
            : SshSigVerificationResult.InvalidSignature;
    }

    private static SshSigVerificationResult VerifySkEd25519Sig(
        byte[] innerSigBytes,
        byte[] sighashbuf,
        byte[] keyBytes,
        ReadOnlySpan<byte> pkSpan,
        int pkPos
    )
    {
        // Read application from public key (after the key bytes)
        string application = SshWireReader.ReadUtf8String(pkSpan, ref pkPos);

        int pos = 0;
        ReadOnlySpan<byte> sigSpan = innerSigBytes;
        string sigType = SshWireReader.ReadUtf8String(sigSpan, ref pos);

        if (!string.Equals(a: sigType, b: "sk-ssh-ed25519@openssh.com", comparisonType: StringComparison.Ordinal))
        {
            return SshSigVerificationResult.InvalidFormat;
        }

        byte[] rawSig = SshWireReader.ReadStringBytes(sigSpan, ref pos);
        byte flags = SshWireReader.ReadByte(sigSpan, ref pos);
        uint counter = SshWireReader.ReadUInt32(sigSpan, ref pos);

        if (rawSig.Length != 64)
        {
            return SshSigVerificationResult.InvalidFormat;
        }

        byte[] appHash = SHA256.HashData(Encoding.UTF8.GetBytes(application));
        byte[] clientDataHash = SHA256.HashData(sighashbuf);
        byte[] authData = new byte[32 + 1 + 4 + 32];
        appHash.CopyTo(authData, 0);
        authData[32] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(authData.AsSpan(start: 33, length: 4), counter);
        clientDataHash.CopyTo(authData, 37);

        return VerifyEd25519(publicKey32: keyBytes, message: authData, signature64: rawSig)
            ? SshSigVerificationResult.Valid
            : SshSigVerificationResult.InvalidSignature;
    }

    private static bool VerifyEd25519(ReadOnlySpan<byte> publicKey32, byte[] message, byte[] signature64)
    {
        if (publicKey32.Length != 32 || signature64.Length != 64)
        {
            return false;
        }

        // Build Ed25519 SubjectPublicKeyInfo DER (44 bytes total)
        Span<byte> spki = stackalloc byte[44];
        Ed25519SpkiPrefix.CopyTo(spki);
        publicKey32.CopyTo(spki[12..]);

        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(spki, out _);

        // For Ed25519, the .NET BCL detects the key type and passes the message directly to
        // OpenSSL's EVP_DigestVerify with NID_null (no pre-hash). The HashAlgorithmName is
        // required by the API signature but is ignored for Ed25519.
        return ecdsa.VerifyData(
            data: message,
            signature: signature64,
            hashAlgorithm: HashAlgorithmName.SHA512,
            signatureFormat: DSASignatureFormat.IeeeP1363FixedFieldConcatenation
        );
    }

    private static byte[] BuildSigHashBuf(string ns, string hashAlgo, byte[] messageBytes)
    {
        byte[] h = hashAlgo switch
        {
            "sha256" => SHA256.HashData(messageBytes),
            "sha512" => SHA512.HashData(messageBytes),
            _ => throw new InvalidDataException($"Unsupported hash algorithm: {hashAlgo}"),
        };

        byte[] nsBytes = Encoding.UTF8.GetBytes(ns);
        byte[] algoBytes = Encoding.UTF8.GetBytes(hashAlgo);

        // "SSHSIG" (6) + uint32(1) (4) + string(ns) (4+len) + string("") (4) + string(algo) (4+len) + string(H(m)) (4+len)
        int totalLen = 6 + 4 + 4 + nsBytes.Length + 4 + 4 + algoBytes.Length + 4 + h.Length;
        byte[] buf = new byte[totalLen];
        int offset = 0;

        "SSHSIG"u8.CopyTo(buf.AsSpan(start: 0, length: 6));
        offset = 6;
        WriteUInt32(buf, ref offset, 1);
        WriteString(buf, ref offset, nsBytes);
        WriteUInt32(buf, ref offset, 0); // empty reserved
        WriteString(buf, ref offset, algoBytes);
        WriteString(buf, ref offset, h);

        return buf;
    }

    private static void WriteUInt32(byte[] buf, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(start: offset, length: 4), value);
        offset += 4;
    }

    private static void WriteString(byte[] buf, ref int offset, byte[] data)
    {
        WriteUInt32(buf, ref offset, (uint)data.Length);
        data.CopyTo(buf, offset);
        offset += data.Length;
    }

    private static byte[] ParsePem(string pem)
    {
        int beginIdx = pem.IndexOf(value: SshSigBegin, comparisonType: StringComparison.Ordinal);
        int endIdx = pem.IndexOf(value: SshSigEnd, comparisonType: StringComparison.Ordinal);

        if (beginIdx < 0 || endIdx < 0 || endIdx <= beginIdx)
        {
            throw new FormatException("Invalid SSH signature PEM format");
        }

        string b64 = pem[(beginIdx + SshSigBegin.Length)..endIdx];
        b64 = b64.Replace(oldValue: "\r\n", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\n", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\r", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: " ", newValue: string.Empty, comparisonType: StringComparison.Ordinal);

        return Convert.FromBase64String(b64);
    }
}
