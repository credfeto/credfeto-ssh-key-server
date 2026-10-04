using System.Diagnostics;

namespace Credfeto.Keys.Services;

[DebuggerDisplay("{Status}")]
public sealed record KeyChallengeResult(KeyManagementStatus Status, KeyChallenge? Challenge)
{
    public static KeyChallengeResult InvalidRequest { get; } =
        new(Status: KeyManagementStatus.InvalidRequest, Challenge: null);

    public static KeyChallengeResult NotFound { get; } = new(Status: KeyManagementStatus.NotFound, Challenge: null);

    public static KeyChallengeResult Success(KeyChallenge challenge)
    {
        return new(Status: KeyManagementStatus.Success, Challenge: challenge);
    }
}
