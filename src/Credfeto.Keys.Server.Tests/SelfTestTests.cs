using System.Threading.Tasks;
using Credfeto.Keys.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class SelfTestTests : TestBase
{
    [Fact]
    public void SelfTestSwitchIsDetected()
    {
        Assert.True(SelfTest.IsSelfTest(["--self-test"]), userMessage: "--self-test should be detected");
    }

    [Fact]
    public void SelfTestSwitchIsDetectedAmongOtherArguments()
    {
        Assert.True(
            SelfTest.IsSelfTest(["--urls", "http://localhost:8080", "--self-test"]),
            userMessage: "--self-test should be detected among other arguments"
        );
    }

    [Fact]
    public void NoArgumentsIsNotSelfTest()
    {
        Assert.False(SelfTest.IsSelfTest([]), userMessage: "No arguments should not be a self-test");
    }

    [Fact]
    public void HealthCheckIsNotSelfTest()
    {
        Assert.False(
            SelfTest.IsSelfTest(["--health-check", "http://127.0.0.1:8080/ping"]),
            userMessage: "A health check should not be a self-test"
        );
    }

    [Fact]
    public void SelfTestSwitchIsCaseSensitive()
    {
        Assert.False(SelfTest.IsSelfTest(["--SELF-TEST"]), userMessage: "The switch should be case sensitive");
    }

    [Fact]
    public async Task SelfTestSucceedsAsync()
    {
        int exitCode = await SelfTest.RunAsync();

        Assert.Equal(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task SelfTestFailsWhenServicesCannotBeCreatedAsync()
    {
        int exitCode = await SelfTest.RunAsync([new(key: "Challenge:HmacSecret", value: "not base64!")]);

        Assert.Equal(expected: 1, actual: exitCode);
    }
}
