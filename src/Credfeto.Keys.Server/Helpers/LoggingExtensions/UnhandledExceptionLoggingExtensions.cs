using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.Server.Helpers.LoggingExtensions;

internal static partial class UnhandledExceptionLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception processing {method} {path}")]
    public static partial void UnhandledException(this ILogger logger, string method, string path, Exception exception);
}
