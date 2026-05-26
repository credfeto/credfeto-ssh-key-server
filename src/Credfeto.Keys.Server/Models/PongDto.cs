using System.Diagnostics;

namespace Credfeto.Keys.Server.Models;

[DebuggerDisplay("Message: {Message}")]
internal readonly record struct PongDto(string Message);
