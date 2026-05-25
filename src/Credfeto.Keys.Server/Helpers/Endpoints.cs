using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Keys.Server.Helpers;

internal static partial class Endpoints
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        Console.WriteLine("Configuring Test/Ping Endpoint");
        app.MapGet(pattern: "/ping", handler: static () => Results.Ok(PingPong.Model));

        return app.ConfigureKeysEndpoints();
    }
}
