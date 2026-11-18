using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;

namespace Credfeto.Keys.Services.Tests;

public sealed class KeyManagementServiceAdminTests : LoggingTestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private const string KEY_TYPE = "ssh-ed25519";
    private const string KEY_DATA = "AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl";
    private const string FINGERPRINT = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";
    private const string OTHER_KEY_DATA = "AAAA";
    private const string KEY_LINE = KEY_TYPE + " " + KEY_DATA + " mark@laptop";
    private static readonly Guid KeyId = new("12345678-1234-1234-1234-123456789012");
    private static readonly Guid OtherKeyId = new("87654321-4321-4321-4321-210987654321");

    private readonly ISshKeyDataStore _store;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IKeyManagementService _keyManagementService;

    public KeyManagementServiceAdminTests(ITestOutputHelper output)
        : base(output)
    {
        this._store = GetSubstitute<ISshKeyDataStore>();
        this._timeProvider = new();

        this._keyManagementService = new KeyManagementService(
            store: this._store,
            challengeService: GetSubstitute<IChallengeService>(),
            timeProvider: this._timeProvider,
            logger: this.GetTypedLogger<KeyManagementService>()
        );
    }

    [Fact]
    public async Task ListReturnsStoredKeysWithFingerprintsAsync()
    {
        this.MockStoreGetKeys(this.CreateKey(keyId: KeyId, keyData: KEY_DATA, comment: "laptop"));

        AdminKeyResult result = await this._keyManagementService.AdminListKeysAsync(
            host: HOST,
            user: USER,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.Success, actual: result.Status);
        AdminKeyEntry entry = Assert.Single(result.Keys);
        Assert.Equal(
            expected: new AdminKeyEntry(KeyId: KeyId, Fingerprint: FINGERPRINT, KeyType: KEY_TYPE, Comment: "laptop"),
            actual: entry
        );
    }

    [Fact]
    public async Task ListReportsUnreadableKeyDataAsInvalidFingerprintAsync()
    {
        this.MockStoreGetKeys(this.CreateKey(keyId: KeyId, keyData: "!!!!", comment: "broken"));

        AdminKeyResult result = await this._keyManagementService.AdminListKeysAsync(
            host: HOST,
            user: USER,
            cancellationToken: this.CancellationToken()
        );

        AdminKeyEntry entry = Assert.Single(result.Keys);
        Assert.Equal(expected: "(invalid-key-data)", actual: entry.Fingerprint);
    }

    [Theory]
    [InlineData("bad host!", USER)]
    [InlineData(HOST, "bad user!")]
    public async Task AdminOperationsRejectInvalidHostOrUserAsync(string host, string user)
    {
        AdminKeyResult[] results =
        [
            await this._keyManagementService.AdminListKeysAsync(
                host: host,
                user: user,
                cancellationToken: this.CancellationToken()
            ),
            await this._keyManagementService.AdminAddKeyAsync(
                host: host,
                user: user,
                keyLine: KEY_LINE,
                cancellationToken: this.CancellationToken()
            ),
            await this._keyManagementService.AdminDeleteKeyAsync(
                host: host,
                user: user,
                keyLine: KEY_LINE,
                cancellationToken: this.CancellationToken()
            ),
            await this._keyManagementService.AdminDeleteKeyByFingerprintAsync(
                host: host,
                user: user,
                fingerprint: FINGERPRINT,
                cancellationToken: this.CancellationToken()
            ),
        ];

        Assert.All(collection: results, action: result =>
        {
            Assert.Equal(expected: KeyManagementStatus.InvalidRequest, actual: result.Status);
            Assert.Empty(result.Keys);
        });
        await this.ReceivedNoStoreReadsAsync();
    }

    [Fact]
    public async Task AddStoresNewKeyAsync()
    {
        this.MockStoreGetKeys(this.CreateKey(keyId: OtherKeyId, keyData: OTHER_KEY_DATA, comment: "other"));
        this.MockStoreAddKey(this.CreateKey(keyId: KeyId, keyData: KEY_DATA, comment: "mark@laptop"));

        AdminKeyResult result = await this._keyManagementService.AdminAddKeyAsync(
            host: HOST,
            user: USER,
            keyLine: "  " + KEY_LINE + "  ",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.Success, actual: result.Status);
        AdminKeyEntry entry = Assert.Single(result.Keys);
        Assert.Equal(expected: FINGERPRINT, actual: entry.Fingerprint);
        await this
            ._store.Received(1)
            .AddKeyAsync(
                host: HOST,
                username: USER,
                keyType: KEY_TYPE,
                keyData: KEY_DATA,
                comment: "mark@laptop",
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task AddOfExistingKeyChangesNothingAsync()
    {
        this.MockStoreGetKeys(this.CreateKey(keyId: KeyId, keyData: KEY_DATA, comment: "original comment"));

        AdminKeyResult result = await this._keyManagementService.AdminAddKeyAsync(
            host: HOST,
            user: USER,
            keyLine: KEY_TYPE + " " + KEY_DATA + " a different comment",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.AlreadyExists, actual: result.Status);
        AdminKeyEntry entry = Assert.Single(result.Keys);
        Assert.Equal(expected: KeyId, actual: entry.KeyId);
        Assert.Equal(expected: "original comment", actual: entry.Comment);
        await this.ReceivedNoStoreAddAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ssh-ed25519")]
    [InlineData("ssh-rsa AAAA")]
    [InlineData("ssh-ed25519 not-base64!")]
    public async Task AddRejectsInvalidKeyAsync(string keyLine)
    {
        AdminKeyResult result = await this._keyManagementService.AdminAddKeyAsync(
            host: HOST,
            user: USER,
            keyLine: keyLine,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.InvalidKey, actual: result.Status);
        await this.ReceivedNoStoreReadsAsync();
        await this.ReceivedNoStoreAddAsync();
    }

    [Fact]
    public async Task DeleteByKeyIgnoresCommentAndRemovesEveryMatchAsync()
    {
        Guid duplicateKeyId = Guid.NewGuid();
        this.MockStoreGetKeys(
            this.CreateKey(keyId: KeyId, keyData: KEY_DATA, comment: "first"),
            this.CreateKey(keyId: OtherKeyId, keyData: OTHER_KEY_DATA, comment: "other"),
            this.CreateKey(keyId: duplicateKeyId, keyData: KEY_DATA, comment: "second")
        );
        this.MockStoreRemoveKey(true);

        AdminKeyResult result = await this._keyManagementService.AdminDeleteKeyAsync(
            host: HOST,
            user: USER,
            keyLine: KEY_TYPE + " " + KEY_DATA + " unrelated comment",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.Success, actual: result.Status);
        Guid[] expectedRemoved = [KeyId, duplicateKeyId];
        Assert.Equal(expected: expectedRemoved, actual: result.Keys.Select(entry => entry.KeyId));
        await this
            ._store.DidNotReceive()
            .RemoveKeyAsync(
                host: HOST,
                username: USER,
                keyId: OtherKeyId,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task DeleteByKeyRejectsInvalidKeyAsync()
    {
        AdminKeyResult result = await this._keyManagementService.AdminDeleteKeyAsync(
            host: HOST,
            user: USER,
            keyLine: "not a key",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.InvalidKey, actual: result.Status);
        await this.ReceivedNoStoreReadsAsync();
    }

    [Fact]
    public async Task DeleteByFingerprintRemovesMatchingKeyAsync()
    {
        this.MockStoreGetKeys(
            this.CreateKey(keyId: KeyId, keyData: KEY_DATA, comment: "laptop"),
            this.CreateKey(keyId: OtherKeyId, keyData: OTHER_KEY_DATA, comment: "other")
        );
        this.MockStoreRemoveKey(true);

        AdminKeyResult result = await this._keyManagementService.AdminDeleteKeyByFingerprintAsync(
            host: HOST,
            user: USER,
            fingerprint: " " + FINGERPRINT + " ",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.Success, actual: result.Status);
        AdminKeyEntry entry = Assert.Single(result.Keys);
        Assert.Equal(expected: KeyId, actual: entry.KeyId);
    }

    [Fact]
    public async Task DeleteByFingerprintRejectsValueWithoutPrefixAsync()
    {
        AdminKeyResult result = await this._keyManagementService.AdminDeleteKeyByFingerprintAsync(
            host: HOST,
            user: USER,
            fingerprint: "+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.InvalidKey, actual: result.Status);
        await this.ReceivedNoStoreReadsAsync();
    }

    [Fact]
    public async Task DeleteOfMissingKeyReportsNotFoundAsync()
    {
        this.MockStoreGetKeys(this.CreateKey(keyId: OtherKeyId, keyData: OTHER_KEY_DATA, comment: "other"));

        AdminKeyResult result = await this._keyManagementService.AdminDeleteKeyByFingerprintAsync(
            host: HOST,
            user: USER,
            fingerprint: FINGERPRINT,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.NotFound, actual: result.Status);
        Assert.Empty(result.Keys);
    }

    [Fact]
    public async Task DeleteReportsNotFoundWhenStoreRemovesNothingAsync()
    {
        this.MockStoreGetKeys(this.CreateKey(keyId: KeyId, keyData: KEY_DATA, comment: "laptop"));
        this.MockStoreRemoveKey(false);

        AdminKeyResult result = await this._keyManagementService.AdminDeleteKeyAsync(
            host: HOST,
            user: USER,
            keyLine: KEY_LINE,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: KeyManagementStatus.NotFound, actual: result.Status);
    }

    private void MockStoreGetKeys(params SshPublicKey[] keys)
    {
        this._store.ReturnsForAll(_ => ValueTask.FromResult<IReadOnlyList<SshPublicKey>>(keys));
    }

    private void MockStoreAddKey(SshPublicKey added)
    {
        this._store.ReturnsForAll(_ => ValueTask.FromResult(added));
    }

    private void MockStoreRemoveKey(bool removed)
    {
        this._store.ReturnsForAll(_ => ValueTask.FromResult(removed));
    }

    private async ValueTask ReceivedNoStoreReadsAsync()
    {
        await this
            ._store.DidNotReceive()
            .GetKeysAsync(
                host: Arg.Any<string>(),
                username: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    private async ValueTask ReceivedNoStoreAddAsync()
    {
        await this
            ._store.DidNotReceive()
            .AddKeyAsync(
                host: Arg.Any<string>(),
                username: Arg.Any<string>(),
                keyType: Arg.Any<string>(),
                keyData: Arg.Any<string>(),
                comment: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    private SshPublicKey CreateKey(in Guid keyId, string keyData, string comment)
    {
        return new()
        {
            KeyId = keyId,
            KeyType = KEY_TYPE,
            KeyData = keyData,
            Comment = comment,
            AddedAt = this._timeProvider.GetUtcNow(),
        };
    }
}
