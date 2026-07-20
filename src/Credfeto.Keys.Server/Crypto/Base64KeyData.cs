using System;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Keys.Server.Crypto;

public static class Base64KeyData
{
    public static bool TryDecode(string value, [NotNullWhen(true)] out byte[]? bytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            bytes = null;

            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(value);

            return true;
        }
        catch (FormatException)
        {
            bytes = null;

            return false;
        }
    }
}
