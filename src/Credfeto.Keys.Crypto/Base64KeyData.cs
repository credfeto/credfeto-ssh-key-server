using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Credfeto.Keys.Crypto;

public static partial class Base64KeyData
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

    // Stricter than TryDecode: Convert.FromBase64String tolerates embedded whitespace, which is never valid in
    // the key-data field of an authorized_keys line, so only the bare standard alphabet with trailing padding passes.
    public static bool IsStrictBase64(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return StrictBase64Regex().IsMatch(value) && TryDecode(value: value, bytes: out _);
    }

    [GeneratedRegex(
        pattern: "^[A-Za-z0-9+/]+=*$",
        options: RegexOptions.CultureInvariant | RegexOptions.NonBacktracking
    )]
    private static partial Regex StrictBase64Regex();
}
