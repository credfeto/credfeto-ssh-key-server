using System;
using System.Threading;
using Credfeto.Keys.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class ServerStartupTests : TestBase
{
    [Fact]
    public void SetThreadsRaisesMinimumThreadsWhenBelowRequested()
    {
        int requested = Environment.ProcessorCount + 1;

        ServerStartup.SetThreads(requested);

        ThreadPool.GetMinThreads(out int minWorker, out int minIoc);
        Assert.True(minWorker >= requested, userMessage: "Minimum worker threads should be raised");
        Assert.True(minIoc >= requested, userMessage: "Minimum IOC threads should be raised");
    }

    [Fact]
    public void SetThreadsLeavesMinimumThreadsWhenAlreadySufficient()
    {
        ThreadPool.GetMinThreads(out int originalWorker, out int originalIoc);

        ServerStartup.SetThreads(1);

        ThreadPool.GetMinThreads(out int minWorker, out int minIoc);
        Assert.Equal(expected: originalWorker, actual: minWorker);
        Assert.Equal(expected: originalIoc, actual: minIoc);
    }
}
