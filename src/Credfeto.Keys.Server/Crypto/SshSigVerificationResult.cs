namespace Credfeto.Keys.Server.Crypto;

public enum SshSigVerificationResult
{
    Valid,
    InvalidFormat,
    InvalidMagic,
    UnsupportedVersion,
    NamespaceMismatch,
    PublicKeyMismatch,
    UnsupportedKeyType,
    UnsupportedHashAlgorithm,
    InvalidSignature,
}
