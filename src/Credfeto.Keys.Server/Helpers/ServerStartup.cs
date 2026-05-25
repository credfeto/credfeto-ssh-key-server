using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using Credfeto.Keys.DataStore.FileSystem;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.Server.Config;
using Credfeto.Keys.Server.Json;
using Credfeto.Keys.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;

namespace Credfeto.Keys.Server.Helpers;

internal static class ServerStartup
{
#if DEBUG
    private const int HTTP_PORT = 9080;
#else
    private const int HTTP_PORT = 8080;
#endif

    public static void SetThreads(int minThreads)
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIoc);

        if (minWorker < minThreads && minIoc < minThreads)
        {
            ThreadPool.SetMinThreads(workerThreads: minThreads, completionPortThreads: minThreads);
        }
        else if (minWorker < minThreads)
        {
            ThreadPool.SetMinThreads(workerThreads: minThreads, completionPortThreads: minIoc);
        }
        else if (minIoc < minThreads)
        {
            ThreadPool.SetMinThreads(workerThreads: minWorker, completionPortThreads: minThreads);
        }
    }

    public static WebApplication CreateApp(string[] args)
    {
        string configPath = ApplicationConfigLocator.ConfigurationFilesPath;

        return WebApplication
            .CreateSlimBuilder(args)
            .ConfigureSettings(configPath)
            .ConfigureServices()
            .ConfigureAppHost()
            .ConfigureWebHost()
            .Build();
    }

    private static WebApplicationBuilder ConfigureAppHost(this WebApplicationBuilder builder)
    {
        builder.Host.UseWindowsService().UseSystemd();

        return builder;
    }

    private static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {
        IConfigurationSection keysSection = builder.Configuration.GetSection("Keys");
        IConfigurationSection challengeSection = builder.Configuration.GetSection("Challenge");

        builder
            .Services.Configure<FileSystemKeyStoreOptions>(keysSection)
            .Configure<ChallengeOptions>(challengeSection)
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddFileSystemKeyStorage()
            .AddSingleton<IChallengeService, ChallengeService>()
            .ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.TypeInfoResolverChain.Insert(index: 0, item: AppJsonContexts.Default)
            );

        return builder;
    }

    private static WebApplicationBuilder ConfigureWebHost(this WebApplicationBuilder builder)
    {
        builder
            .WebHost.UseKestrel(options: options => SetKestrelOptions(options: options, httpPort: HTTP_PORT))
            .UseSetting(key: WebHostDefaults.SuppressStatusMessagesKey, value: "True")
            .ConfigureLogging((_, logger) => ConfigureLogging(logger));

        return builder;
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2000:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    private static void ConfigureLogging(ILoggingBuilder logger)
    {
        logger.ClearProviders().AddSerilog(CreateLogger(), dispose: true);
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty(name: "ServerVersion", value: VersionInformation.Version)
            .Enrich.WithProperty(name: "ProcessName", value: VersionInformation.Product)
            .WriteToDebuggerAwareOutput()
            .CreateLogger();
    }

    private static LoggerConfiguration WriteToDebuggerAwareOutput(this LoggerConfiguration configuration)
    {
        LoggerSinkConfiguration writeTo = configuration.WriteTo;

        return Debugger.IsAttached ? writeTo.Debug() : writeTo.Console();
    }

    private static WebApplicationBuilder ConfigureSettings(this WebApplicationBuilder builder, string configPath)
    {
        builder.Configuration.Sources.Clear();
        builder
            .Configuration.SetBasePath(configPath)
            .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(path: "appsettings-local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        return builder;
    }

    private static void SetKestrelOptions(KestrelServerOptions options, int httpPort)
    {
        options.DisableStringReuse = false;
        options.AllowSynchronousIO = false;
        options.AddServerHeader = false;
        options.Limits.MinResponseDataRate = null;
        options.Limits.MinRequestBodyDataRate = null;

        Console.WriteLine($"Listening on HTTP port: {httpPort}");
        options.Listen(
            address: IPAddress.Any,
            port: httpPort,
            configure: static o => o.Protocols = HttpProtocols.Http1
        );
    }
}
