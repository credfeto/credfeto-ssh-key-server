using System;
using Credfeto.Keys.Server.Config;
using Credfeto.Keys.Server.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class ChallengeServiceTests : LoggingTestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private static readonly Guid KeyId = new("12345678-1234-1234-1234-123456789012");
    private static readonly byte[] Secret = new byte[32];

    public ChallengeServiceTests(ITestOutputHelper output)
        : base(output) { }

    private IChallengeService CreateService(FakeTimeProvider? timeProvider = null)
    {
        IOptions<ChallengeOptions> options = Substitute.For<IOptions<ChallengeOptions>>();
        options.Value.Returns(
            new ChallengeOptions
            {
                HmacSecret = Convert.ToBase64String(Secret),
                TtlSeconds = 300,
                SshNamespace = "ssh-key-server-v1",
            }
        );

        return new ChallengeService(
            options: options,
            timeProvider: timeProvider ?? TimeProvider.System,
            logger: this.GetTypedLogger<ChallengeService>()
        );
    }

    [Fact]
    public void AddChallengeCanBeVerified()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.Valid, actual: result);
    }

    [Fact]
    public void DeleteChallengeCanBeVerified()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId);

        ChallengeVerificationResult result = service.VerifyDeleteChallenge(
            host: HOST,
            user: USER,
            keyId: KeyId,
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.Valid, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForWrongHost()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(
            host: "other.example.com",
            user: USER,
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.ContextMismatch, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForWrongUser()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: "other", token: token);

        Assert.Equal(expected: ChallengeVerificationResult.ContextMismatch, actual: result);
    }

    [Fact]
    public void DeleteChallengeFailsForWrongKeyId()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId);

        ChallengeVerificationResult result = service.VerifyDeleteChallenge(
            host: HOST,
            user: USER,
            keyId: Guid.NewGuid(),
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.ContextMismatch, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForTamperedToken()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);
        string tampered = token[..^4] + "XXXX";

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: tampered);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidSignature, actual: result);
    }

    [Fact]
    public void AddChallengeFailsWhenExpired()
    {
        FakeTimeProvider timeProvider = new();
        IChallengeService service = this.CreateService(timeProvider);
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        timeProvider.Advance(TimeSpan.FromSeconds(301));

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.Expired, actual: result);
    }

    [Fact]
    public void AddChallengeCannotBeUsedAsDeleteChallenge()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyDeleteChallenge(
            host: HOST,
            user: USER,
            keyId: KeyId,
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void DeleteChallengeCannotBeUsedAsAddChallenge()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void SshNamespaceMatchesConfiguration()
    {
        IChallengeService service = this.CreateService();

        Assert.Equal(expected: "ssh-key-server-v1", actual: service.SshNamespace);
    }
}
