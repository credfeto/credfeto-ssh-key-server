using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Crypto.Tests;

public sealed class SshKeyFingerprintTests : TestBase
{
    // GitHub's published ed25519 host key and the fingerprint GitHub documents for it.
    private const string GITHUB_HOST_KEY_DATA = "AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl";
    private const string GITHUB_HOST_KEY_FINGERPRINT = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";

    [Fact]
    public void TryComputeMatchesPublishedFingerprint()
    {
        bool result = SshKeyFingerprint.TryCompute(keyData: GITHUB_HOST_KEY_DATA, fingerprint: out string? fingerprint);

        Assert.True(condition: result, userMessage: "Expected valid key data to produce a fingerprint");
        Assert.Equal(expected: GITHUB_HOST_KEY_FINGERPRINT, actual: fingerprint);
    }

    [Fact]
    public void TryComputeFailsForInvalidKeyData()
    {
        bool result = SshKeyFingerprint.TryCompute(keyData: "!!!!", fingerprint: out string? fingerprint);

        Assert.False(condition: result, userMessage: "Expected invalid key data to be rejected");
        Assert.Null(fingerprint);
    }
}
