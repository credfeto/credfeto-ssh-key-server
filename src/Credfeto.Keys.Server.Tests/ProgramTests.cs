using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class ProgramTests : TestBase
{
    [Fact]
    public async Task SelfTestSwitchRunsSelfTestAsync()
    {
        int exitCode = await Program.Main(["--self-test"]);

        Assert.Equal(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task HealthCheckAgainstUnreachableServerFailsAsync()
    {
        int exitCode = await Program.Main(["--health-check", "http://127.0.0.1:1/ping"]);

        Assert.NotEqual(expected: 0, actual: exitCode);
    }
}
