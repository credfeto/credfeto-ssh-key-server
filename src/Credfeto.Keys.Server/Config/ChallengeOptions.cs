using System.Diagnostics;

namespace Credfeto.Keys.Server.Config;

[DebuggerDisplay("TtlSeconds: {TtlSeconds}, Namespace: {SshNamespace}")]
public sealed class ChallengeOptions
{
    // Must be a plain settable (not init) property: the Configuration Binding
    // Source Generator constructs a default instance and assigns each bound
    // property afterwards, which init-only members reject at compile time —
    // under Native AOT that silently leaves this unset instead of failing the
    // build, so the app starts with an empty HmacSecret even when the
    // environment variable is set correctly.
    public string? HmacSecret { get; set; }

    public int TtlSeconds { get; set; } = 300;

    public string SshNamespace { get; set; } = "ssh-key-server-v1";
}
