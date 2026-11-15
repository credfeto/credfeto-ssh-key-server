using System;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;

namespace Credfeto.Keys.Services.Tests;

public sealed class KeyManagementServiceTests : LoggingTestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private const string CHALLENGE_TOKEN = "challenge-token";
    private const string SSH_NAMESPACE = "ssh-key-server-v1";
    private static readonly Guid KeyId = new("12345678-1234-1234-1234-123456789012");
    private static readonly TimeSpan ConfiguredTtl = TimeSpan.FromSeconds(60);

    private readonly IChallengeService _challengeService;
    private readonly ISshKeyDataStore _store;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IKeyManagementService _keyManagementService;

    public KeyManagementServiceTests(ITestOutputHelper output)
        : base(output)
    {
        this._challengeService = GetSubstitute<IChallengeService>();
        this._challengeService.SshNamespace.Returns(SSH_NAMESPACE);
        this._challengeService.Ttl.Returns(ConfiguredTtl);

        this._store = GetSubstitute<ISshKeyDataStore>();
        this._timeProvider = new();

        this._keyManagementService = new KeyManagementService(
            store: this._store,
            challengeService: this._challengeService,
            timeProvider: this._timeProvider,
            logger: this.GetTypedLogger<KeyManagementService>()
        );
    }

    [Fact]
    public void AddChallengeValidUntilUsesConfiguredTtl()
    {
        this._challengeService.GenerateAddChallenge(host: HOST, user: USER).Returns(CHALLENGE_TOKEN);

        KeyChallengeResult result = this._keyManagementService.GenerateAddChallenge(host: HOST, user: USER);

        this.AssertChallengeIssuedWithConfiguredTtl(result);
    }

    [Fact]
    public async Task DeleteChallengeValidUntilUsesConfiguredTtlAsync()
    {
        SshPublicKey key = this.CreateKey();
        this._store.ReturnsForAll(_ => ValueTask.FromResult<SshPublicKey?>(key));
        this._challengeService.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId).Returns(CHALLENGE_TOKEN);

        KeyChallengeResult result = await this._keyManagementService.GenerateDeleteChallengeAsync(
            host: HOST,
            user: USER,
            keyId: KeyId,
            cancellationToken: this.CancellationToken()
        );

        this.AssertChallengeIssuedWithConfiguredTtl(result);
    }

    private void AssertChallengeIssuedWithConfiguredTtl(KeyChallengeResult result)
    {
        Assert.Equal(expected: KeyManagementStatus.Success, actual: result.Status);
        Assert.NotNull(result.Challenge);
        Assert.Equal(expected: CHALLENGE_TOKEN, actual: result.Challenge.Token);
        Assert.Equal(expected: SSH_NAMESPACE, actual: result.Challenge.Namespace);
        Assert.Equal(expected: this._timeProvider.GetUtcNow().Add(ConfiguredTtl), actual: result.Challenge.ValidUntil);
    }

    private SshPublicKey CreateKey()
    {
        return new()
        {
            KeyId = KeyId,
            KeyType = "ssh-ed25519",
            KeyData = "AAAA",
            Comment = "test",
            AddedAt = this._timeProvider.GetUtcNow(),
        };
    }
}
