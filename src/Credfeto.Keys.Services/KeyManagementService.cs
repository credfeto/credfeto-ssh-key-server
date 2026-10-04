using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.Crypto;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using Credfeto.Keys.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.Services;

public sealed partial class KeyManagementService : IKeyManagementService
{
    private const int CHALLENGE_VALIDITY_SECONDS = 300;

    private static readonly string[] ValidKeyTypes = ["ssh-ed25519", "sk-ssh-ed25519@openssh.com"];

    private readonly IChallengeService _challengeService;
    private readonly ILogger<KeyManagementService> _logger;
    private readonly ISshKeyDataStore _store;
    private readonly TimeProvider _timeProvider;

    public KeyManagementService(
        ISshKeyDataStore store,
        IChallengeService challengeService,
        TimeProvider timeProvider,
        ILogger<KeyManagementService> logger
    )
    {
        this._store = store;
        this._challengeService = challengeService;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    public async ValueTask<KeyListResult> GetKeysAsync(string host, string user, CancellationToken cancellationToken)
    {
        if (!IsValidHostAndUser(host: host, user: user))
        {
            return KeyListResult.InvalidRequest;
        }

        IReadOnlyList<SshPublicKey> keys = await this._store.GetKeysAsync(
            host: host,
            username: user,
            cancellationToken: cancellationToken
        );

        return KeyListResult.Success(BuildAuthorizedKeys(keys));
    }

    public KeyChallengeResult GenerateAddChallenge(string host, string user)
    {
        if (!IsValidHostAndUser(host: host, user: user))
        {
            return KeyChallengeResult.InvalidRequest;
        }

        string token = this._challengeService.GenerateAddChallenge(host: host, user: user);

        return KeyChallengeResult.Success(this.CreateChallenge(token));
    }

    public async ValueTask<AddKeyResult> AddKeyAsync(
        string host,
        string user,
        string key,
        string challenge,
        string signature,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHostAndUser(host: host, user: user))
        {
            return AddKeyResult.Failed(KeyManagementStatus.InvalidRequest);
        }

        ChallengeVerificationResult challengeResult = this._challengeService.VerifyAddChallenge(
            host: host,
            user: user,
            token: challenge
        );

        if (challengeResult != ChallengeVerificationResult.Valid)
        {
            this._logger.ChallengeInvalid(host: host, user: user, operation: "add", result: challengeResult);

            return AddKeyResult.Failed(KeyManagementStatus.InvalidChallenge);
        }

        if (
            !TryParseKeyLine(
                keyLine: key.Trim(),
                keyType: out string keyType,
                keyData: out string keyData,
                comment: out string comment
            )
        )
        {
            return AddKeyResult.Failed(KeyManagementStatus.InvalidKey);
        }

        if (
            !this.IsValidSignature(
                host: host,
                user: user,
                keyId: null,
                operation: "add",
                signature: signature,
                challenge: challenge,
                keyType: keyType,
                keyData: keyData
            )
        )
        {
            return AddKeyResult.Failed(KeyManagementStatus.InvalidSignature);
        }

        SshPublicKey added = await this._store.AddKeyAsync(
            host: host,
            username: user,
            keyType: keyType,
            keyData: keyData,
            comment: comment,
            cancellationToken: cancellationToken
        );

        return AddKeyResult.Created(added.KeyId);
    }

    public async ValueTask<KeyChallengeResult> GenerateDeleteChallengeAsync(
        string host,
        string user,
        Guid keyId,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHostAndUser(host: host, user: user))
        {
            return KeyChallengeResult.InvalidRequest;
        }

        SshPublicKey? key = await this._store.GetKeyByIdAsync(
            host: host,
            username: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        if (key is null)
        {
            return KeyChallengeResult.NotFound;
        }

        string token = this._challengeService.GenerateDeleteChallenge(host: host, user: user, keyId: keyId);

        return KeyChallengeResult.Success(this.CreateChallenge(token));
    }

    public async ValueTask<KeyManagementStatus> DeleteKeyAsync(
        string host,
        string user,
        Guid keyId,
        string challenge,
        string signature,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHostAndUser(host: host, user: user))
        {
            return KeyManagementStatus.InvalidRequest;
        }

        ChallengeVerificationResult challengeResult = this._challengeService.VerifyDeleteChallenge(
            host: host,
            user: user,
            keyId: keyId,
            token: challenge
        );

        if (challengeResult != ChallengeVerificationResult.Valid)
        {
            this._logger.ChallengeInvalid(host: host, user: user, operation: "del", result: challengeResult);

            return KeyManagementStatus.InvalidChallenge;
        }

        SshPublicKey? key = await this._store.GetKeyByIdAsync(
            host: host,
            username: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        if (key is null)
        {
            return KeyManagementStatus.NotFound;
        }

        if (
            !this.IsValidSignature(
                host: host,
                user: user,
                keyId: keyId.ToString("D"),
                operation: "del",
                signature: signature,
                challenge: challenge,
                keyType: key.KeyType,
                keyData: key.KeyData
            )
        )
        {
            return KeyManagementStatus.InvalidSignature;
        }

        bool removed = await this._store.RemoveKeyAsync(
            host: host,
            username: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        return removed ? KeyManagementStatus.Success : KeyManagementStatus.NotFound;
    }

    private bool IsValidSignature(
        string host,
        string user,
        string? keyId,
        string operation,
        string signature,
        string challenge,
        string keyType,
        string keyData
    )
    {
        SshSigVerificationResult sigResult = SshSigVerifier.Verify(
            sshSigPem: signature,
            challenge: challenge,
            expectedKeyType: keyType,
            expectedKeyDataBase64: keyData,
            expectedNamespace: this._challengeService.SshNamespace
        );

        if (sigResult == SshSigVerificationResult.Valid)
        {
            return true;
        }

        this._logger.SshSignatureInvalid(host: host, user: user, keyId: keyId, operation: operation, result: sigResult);

        return false;
    }

    private KeyChallenge CreateChallenge(string token)
    {
        return new(
            Token: token,
            Namespace: this._challengeService.SshNamespace,
            ValidUntil: this._timeProvider.GetUtcNow().AddSeconds(CHALLENGE_VALIDITY_SECONDS)
        );
    }

    private static string BuildAuthorizedKeys(IReadOnlyList<SshPublicKey> keys)
    {
        if (keys.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new();

        foreach (SshPublicKey key in keys)
        {
            sb.Append(key.KeyType).Append(' ').Append(key.KeyData);

            if (!string.IsNullOrEmpty(key.Comment))
            {
                sb.Append(' ').Append(key.Comment);
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static bool TryParseKeyLine(string keyLine, out string keyType, out string keyData, out string comment)
    {
        keyType = string.Empty;
        keyData = string.Empty;
        comment = string.Empty;

        if (string.IsNullOrWhiteSpace(keyLine))
        {
            return false;
        }

        string[] parts = keyLine.Split(separator: ' ', count: 3, options: StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return false;
        }

        string type = parts[0];
        string data = parts[1];

        if (!IsValidKeyType(type) || !Base64KeyData.IsStrictBase64(data))
        {
            return false;
        }

        keyType = type;
        keyData = data;
        comment = parts.Length > 2 ? parts[2] : string.Empty;

        return true;
    }

    private static bool IsValidKeyType(string keyType)
    {
        return ValidKeyTypes.Any(valid =>
            string.Equals(a: keyType, b: valid, comparisonType: StringComparison.Ordinal)
        );
    }

    private static bool IsValidHostAndUser(string host, string user)
    {
        return IsValidHost(host) && IsValidUsername(user);
    }

    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrEmpty(host) || host.Length > 253)
        {
            return false;
        }

        return HostRegex().IsMatch(host);
    }

    private static bool IsValidUsername(string username)
    {
        if (string.IsNullOrEmpty(username) || username.Length > 32)
        {
            return false;
        }

        return UsernameRegex().IsMatch(username);
    }

    [GeneratedRegex(
        pattern: @"^[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking
    )]
    private static partial Regex HostRegex();

    [GeneratedRegex(
        pattern: @"^[A-Za-z0-9_\-]{1,32}$",
        options: RegexOptions.CultureInvariant | RegexOptions.NonBacktracking
    )]
    private static partial Regex UsernameRegex();
}
