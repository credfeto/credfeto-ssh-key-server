using System;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.DataStore.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Keys.DataStore.FileSystem.Tests;

public sealed class FileSystemSetupTests : DependencyInjectionTestsBase
{
    public FileSystemSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddFileSystemKeyStorage()
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddMockedService<IOptions<FileSystemKeyStoreOptions>>(static o =>
                o.Value.Returns(new FileSystemKeyStoreOptions { BasePath = "/tmp/test-keys" })
            );
    }

    [Fact]
    public void ISshKeyDataStoreShouldBeRegistered()
    {
        this.RequireService<ISshKeyDataStore>();
    }
}
