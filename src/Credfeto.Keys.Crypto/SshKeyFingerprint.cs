using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Credfeto.Keys.Crypto;

public static class SshKeyFingerprint
{
    private const string PREFIX = "SHA256:";

    // Matches `ssh-keygen -lf`: SHA-256 over the decoded key blob, base64 encoded without padding.
    public static bool TryCompute(string keyData, [NotNullWhen(true)] out string? fingerprint)
    {
        if (!Base64KeyData.TryDecode(value: keyData, bytes: out byte[]? blob))
        {
            fingerprint = null;

            return false;
        }

        fingerprint = PREFIX + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');

        return true;
    }
}
