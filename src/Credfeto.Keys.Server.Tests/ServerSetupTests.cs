using System;
using Credfeto.Keys.DataStore.FileSystem;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.Server.Config;
using Credfeto.Keys.Server.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class ServerSetupTests : DependencyInjectionTestsBase
{
    public ServerSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddFileSystemKeyStorage()
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton<IChallengeService, ChallengeService>()
            .AddMockedService<IOptions<FileSystemKeyStoreOptions>>(static o =>
                o.Value.Returns(new FileSystemKeyStoreOptions { BasePath = "/tmp/test-keys" })
            )
            .AddMockedService<IOptions<ChallengeOptions>>(static o =>
                o.Value.Returns(
                    new ChallengeOptions
                    {
                        HmacSecret = Convert.ToBase64String(new byte[32]),
                        TtlSeconds = 300,
                        SshNamespace = "ssh-key-server-v1",
                    }
                )
            );
    }

    [Fact]
    public void ISshKeyDataStoreShouldBeRegistered()
    {
        this.RequireService<ISshKeyDataStore>();
    }

    [Fact]
    public void IChallengeServiceShouldBeRegistered()
    {
        this.RequireService<IChallengeService>();
    }
}
