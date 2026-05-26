namespace Credfeto.Keys.Server.Crypto;

internal enum SshSigVerificationResult
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
