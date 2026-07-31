using System;
using System.Threading.Tasks;
using Credfeto.Keys.Server.Helpers.LoggingExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.Server.Helpers;

internal static partial class Endpoints
{
    private const string UnhandledExceptionLoggerCategory = "Credfeto.Keys.Server.UnhandledException";

    public static WebApplication UseUnhandledExceptionLogging(this WebApplication app)
    {
        app.UseExceptionHandler(configure: errorApp => errorApp.Run(HandleUnhandledExceptionAsync));

        return app;
    }

    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        Console.WriteLine("Configuring Test/Ping Endpoint");
        app.MapGet(pattern: "/ping", handler: static () => Results.Ok(PingPong.Model));

        return app.ConfigureKeysEndpoints();
    }

    private static Task HandleUnhandledExceptionAsync(HttpContext context)
    {
        IExceptionHandlerFeature? feature = context.Features.Get<IExceptionHandlerFeature>();

        if (feature?.Error is { } exception)
        {
            ILogger logger = context
                .RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(UnhandledExceptionLoggerCategory);

            logger.UnhandledException(
                method: context.Request.Method,
                path: context.Request.Path.Value ?? string.Empty,
                exception: exception
            );
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsync(
            text: """{"error":"Internal Server Error"}""",
            cancellationToken: context.RequestAborted
        );
    }
}
