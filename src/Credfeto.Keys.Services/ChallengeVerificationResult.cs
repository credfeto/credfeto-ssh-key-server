namespace Credfeto.Keys.Services;

public enum ChallengeVerificationResult
{
    Valid,
    InvalidFormat,
    InvalidSignature,
    Expired,
    ContextMismatch,
}
