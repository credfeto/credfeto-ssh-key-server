namespace Credfeto.Keys.Services;

public enum KeyManagementStatus
{
    Success,
    InvalidRequest,
    InvalidChallenge,
    InvalidKey,
    InvalidSignature,
    NotFound,
}
