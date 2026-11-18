using System;
using System.Diagnostics;

namespace Credfeto.Keys.Services;

[DebuggerDisplay("{Fingerprint} {KeyType} {Comment}")]
public sealed record AdminKeyEntry(Guid KeyId, string Fingerprint, string KeyType, string Comment);
