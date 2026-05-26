using System;
using Credfeto.Keys.Server.Crypto;
using Credfeto.Keys.Server.Services;
using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.Server.Helpers.LoggingExtensions;

internal static partial class KeysEndpointsLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Challenge invalid for {operation} on {host}/{user}: {result}"
    )]
    public static partial void ChallengeInvalid(
        this ILogger logger,
        string host,
        string user,
        string operation,
        ChallengeVerificationResult result
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "SSH signature invalid for {operation} on {host}/{user}/{keyId}: {result}"
    )]
    public static partial void SshSignatureInvalid(
        this ILogger logger,
        string host,
        string user,
        string? keyId,
        string operation,
        SshSigVerificationResult result
    );
}
