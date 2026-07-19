using Credfeto.Keys.Server.Crypto;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class Base64KeyDataTests : TestBase
{
    [Fact]
    public void TryDecodeSucceedsForValidBase64()
    {
        bool result = Base64KeyData.TryDecode("AAAA", bytes: out byte[]? bytes);

        Assert.True(condition: result, userMessage: "Expected valid base64 to decode successfully");
        Assert.NotNull(bytes);
    }

    [Fact]
    public void TryDecodeFailsForLengthNotMultipleOfFour()
    {
        bool result = Base64KeyData.TryDecode("AAAAA", bytes: out byte[]? bytes);

        Assert.False(condition: result, userMessage: "Expected base64 with invalid length to be rejected");
        Assert.Null(bytes);
    }

    [Fact]
    public void TryDecodeFailsForEmptyString()
    {
        bool result = Base64KeyData.TryDecode(string.Empty, bytes: out byte[]? bytes);

        Assert.False(condition: result, userMessage: "Expected empty string to be rejected");
        Assert.Null(bytes);
    }

    [Fact]
    public void TryDecodeFailsForInvalidCharacters()
    {
        bool result = Base64KeyData.TryDecode("!!!!", bytes: out byte[]? bytes);

        Assert.False(condition: result, userMessage: "Expected non-base64 characters to be rejected");
        Assert.Null(bytes);
    }
}
