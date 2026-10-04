using System;
using Credfeto.Keys.DataStore.FileSystem;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.Services;
using Credfeto.Keys.Services.Config;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure;
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
            .AddSingleton(TimeProvider.System)
            .AddKeyServices()
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

    [Fact]
    public void IKeyManagementServiceShouldBeRegistered()
    {
        this.RequireService<IKeyManagementService>();
    }
}
