using System.Diagnostics;

namespace Credfeto.Keys.Server.Cli;

[DebuggerDisplay("{Operation} {Username}@{Host}")]
public sealed record AdminCommand(
    AdminOperation Operation,
    string Host,
    string Username,
    string? Key,
    string? Fingerprint
);
