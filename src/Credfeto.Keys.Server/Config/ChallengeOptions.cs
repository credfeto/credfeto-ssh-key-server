using System.Diagnostics;

namespace Credfeto.Keys.Server.Config;

[DebuggerDisplay("TtlSeconds: {TtlSeconds}, Namespace: {SshNamespace}")]
public sealed class ChallengeOptions
{
    public string? HmacSecret { get; init; }

    public int TtlSeconds { get; init; } = 300;

    public string SshNamespace { get; init; } = "ssh-key-server-v1";
}
