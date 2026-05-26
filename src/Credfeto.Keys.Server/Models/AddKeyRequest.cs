using System.Diagnostics;

namespace Credfeto.Keys.Server.Models;

[DebuggerDisplay("Key: {Key}")]
internal sealed class AddKeyRequest
{
    public string Key { get; init; } = string.Empty;

    public string Challenge { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;
}
