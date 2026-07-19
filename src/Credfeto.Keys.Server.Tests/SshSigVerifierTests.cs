using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Credfeto.Keys.Server.Crypto;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class SshSigVerifierTests : TestBase
{
    private const string Namespace = "ssh-key-server-v1";

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

    private static string BuildSshSigPem(string keyType)
    {
        byte[] pubKeyBlob = Concat(WireString(keyType), WireBytes(new byte[32]));

        byte[] body = Concat(
            "SSHSIG"u8.ToArray(),
            WireUInt32(1),
            WireBytes(pubKeyBlob),
            WireString(Namespace),
            WireBytes([]),
            WireString("sha256"),
            WireBytes([])
        );

        string base64 = Convert.ToBase64String(body);

        return $"-----BEGIN SSH SIGNATURE-----\n{base64}\n-----END SSH SIGNATURE-----";
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
