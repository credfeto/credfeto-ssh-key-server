using System.Diagnostics;

namespace Credfeto.Keys.Server.Models;

[DebuggerDisplay("Challenge: {Challenge}")]
internal sealed class DeleteKeyRequest
{
    public string Challenge { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;
}
