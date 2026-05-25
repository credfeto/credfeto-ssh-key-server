using Credfeto.Keys.Server.Models;

namespace Credfeto.Keys.Server.Helpers;

internal static class PingPong
{
    public static PongDto Model { get; } = new("Pong!");
}
