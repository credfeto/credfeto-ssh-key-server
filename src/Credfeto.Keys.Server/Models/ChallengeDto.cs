using System;
using System.Diagnostics;

namespace Credfeto.Keys.Server.Models;

[DebuggerDisplay("Namespace: {Namespace}, ValidUntil: {ValidUntil}")]
internal readonly record struct ChallengeDto(string Challenge, string Namespace, DateTimeOffset ValidUntil);
