using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.Server.Services.LoggingExtensions;

internal static partial class ChallengeServiceLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Challenge verification failed for {operation} on {host}/{user}: {reason}"
    )]
    public static partial void ChallengeVerificationFailed(
        this ILogger logger,
        string host,
        string user,
        string operation,
        string reason
    );
}
