using System;
using System.Diagnostics;

namespace Credfeto.Keys.Services;

[DebuggerDisplay("{Namespace}: {Token} until {ValidUntil}")]
public sealed record KeyChallenge(string Token, string Namespace, DateTimeOffset ValidUntil);
