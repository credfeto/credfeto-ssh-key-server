namespace Credfeto.Keys.Server.Services;

public enum ChallengeVerificationResult
{
    Valid,
    InvalidFormat,
    InvalidSignature,
    Expired,
    ContextMismatch,
}
