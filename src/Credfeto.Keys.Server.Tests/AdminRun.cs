using System.Diagnostics;
using Credfeto.Keys.Server.Cli;

namespace Credfeto.Keys.Server.Tests;

[DebuggerDisplay("{ExitCode}")]
internal sealed record AdminRun(AdminExitCode ExitCode, string Output, string Error);
