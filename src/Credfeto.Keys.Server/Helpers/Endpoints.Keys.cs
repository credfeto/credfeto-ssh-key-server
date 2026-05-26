using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using Credfeto.Keys.Server.Crypto;
using Credfeto.Keys.Server.Helpers.LoggingExtensions;
using Credfeto.Keys.Server.Models;
using Credfeto.Keys.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.Server.Helpers;

internal static partial class Endpoints
{
    private const string KeysLoggerCategory = "Credfeto.Keys.Server.Keys";

    private static readonly string[] ValidKeyTypes = ["ssh-ed25519", "sk-ssh-ed25519@openssh.com"];

    private static WebApplication ConfigureKeysEndpoints(this WebApplication app)
    {
        Console.WriteLine("Configuring SSH Keys Endpoints");

        app.MapGet(pattern: "/keys/{host}/{user}", handler: GetKeysAsync);
        app.MapGet(pattern: "/keys/{host}/{user}/add-challenge", handler: GetAddChallengeAsync);
        app.MapPost(pattern: "/keys/{host}/{user}", handler: AddKeyAsync);
        app.MapGet(pattern: "/keys/{host}/{user}/{keyId}/challenge", handler: GetDeleteChallengeAsync);
        app.MapDelete(pattern: "/keys/{host}/{user}/{keyId}", handler: DeleteKeyAsync);

        return app;
    }

    private static async ValueTask<IResult> GetKeysAsync(
        string host,
        string user,
        ISshKeyDataStore store,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return Results.BadRequest();
        }

        IReadOnlyList<SshPublicKey> keys = await store.GetKeysAsync(
            host: host,
            username: user,
            cancellationToken: cancellationToken
        );

        if (keys.Count == 0)
        {
            return Results.Ok(string.Empty);
        }

        StringBuilder sb = new();

        foreach (SshPublicKey key in keys)
        {
            sb.Append(key.KeyType);
            sb.Append(' ');
            sb.Append(key.KeyData);

            if (!string.IsNullOrEmpty(key.Comment))
            {
                sb.Append(' ');
                sb.Append(key.Comment);
            }

            sb.Append('\n');
        }

        return Results.Text(sb.ToString(), contentType: "text/plain");
    }

    private static ValueTask<IResult> GetAddChallengeAsync(
        string host,
        string user,
        IChallengeService challengeService,
        TimeProvider timeProvider
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return ValueTask.FromResult(Results.BadRequest());
        }

        string token = challengeService.GenerateAddChallenge(host: host, user: user);
        DateTimeOffset validUntil = timeProvider.GetUtcNow().AddSeconds(300);

        return ValueTask.FromResult(
            Results.Ok(
                new ChallengeDto(Challenge: token, Namespace: challengeService.SshNamespace, ValidUntil: validUntil)
            )
        );
    }

    private static async ValueTask<IResult> AddKeyAsync(
        string host,
        string user,
        [FromBody] AddKeyRequest request,
        ISshKeyDataStore store,
        IChallengeService challengeService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return Results.BadRequest("Invalid host or username.");
        }

        ILogger logger = loggerFactory.CreateLogger(KeysLoggerCategory);

        ChallengeVerificationResult challengeResult = challengeService.VerifyAddChallenge(
            host: host,
            user: user,
            token: request.Challenge
        );

        if (challengeResult != ChallengeVerificationResult.Valid)
        {
            logger.ChallengeInvalid(host: host, user: user, operation: "add", result: challengeResult);

            return Results.BadRequest("Invalid or expired challenge.");
        }

        if (
            !TryParseKeyLine(
                keyLine: request.Key.Trim(),
                keyType: out string? keyType,
                keyData: out string? keyData,
                comment: out string? comment
            )
        )
        {
            return Results.BadRequest("Invalid SSH public key format.");
        }

        SshSigVerificationResult sigResult = SshSigVerifier.Verify(
            sshSigPem: request.Signature,
            challenge: request.Challenge,
            expectedKeyType: keyType,
            expectedKeyDataBase64: keyData,
            expectedNamespace: challengeService.SshNamespace
        );

        if (sigResult != SshSigVerificationResult.Valid)
        {
            logger.SshSignatureInvalid(host: host, user: user, keyId: null, operation: "add", result: sigResult);

            return Results.BadRequest("SSH signature verification failed.");
        }

        SshPublicKey added = await store.AddKeyAsync(
            host: host,
            username: user,
            keyType: keyType,
            keyData: keyData,
            comment: comment,
            cancellationToken: cancellationToken
        );

        return Results.Created(uri: (string?)null, value: new AddKeyResponse(added.KeyId));
    }

    private static async ValueTask<IResult> GetDeleteChallengeAsync(
        string host,
        string user,
        Guid keyId,
        ISshKeyDataStore store,
        IChallengeService challengeService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return Results.BadRequest();
        }

        SshPublicKey? key = await store.GetKeyByIdAsync(
            host: host,
            username: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        if (key is null)
        {
            return Results.NotFound();
        }

        string token = challengeService.GenerateDeleteChallenge(host: host, user: user, keyId: keyId);
        DateTimeOffset validUntil = timeProvider.GetUtcNow().AddSeconds(300);

        return Results.Ok(
            new ChallengeDto(Challenge: token, Namespace: challengeService.SshNamespace, ValidUntil: validUntil)
        );
    }

    private static async ValueTask<IResult> DeleteKeyAsync(
        string host,
        string user,
        Guid keyId,
        [FromBody] DeleteKeyRequest request,
        ISshKeyDataStore store,
        IChallengeService challengeService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return Results.BadRequest();
        }

        ILogger logger = loggerFactory.CreateLogger(KeysLoggerCategory);

        ChallengeVerificationResult challengeResult = challengeService.VerifyDeleteChallenge(
            host: host,
            user: user,
            keyId: keyId,
            token: request.Challenge
        );

        if (challengeResult != ChallengeVerificationResult.Valid)
        {
            logger.ChallengeInvalid(host: host, user: user, operation: "del", result: challengeResult);

            return Results.BadRequest("Invalid or expired challenge.");
        }

        return await VerifySignatureAndDeleteAsync(
            host: host,
            user: user,
            keyId: keyId,
            request: request,
            store: store,
            challengeService: challengeService,
            logger: logger,
            cancellationToken: cancellationToken
        );
    }

    private static async ValueTask<IResult> VerifySignatureAndDeleteAsync(
        string host,
        string user,
        Guid keyId,
        DeleteKeyRequest request,
        ISshKeyDataStore store,
        IChallengeService challengeService,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        SshPublicKey? key = await store.GetKeyByIdAsync(
            host: host,
            username: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        if (key is null)
        {
            return Results.NotFound();
        }

        SshSigVerificationResult sigResult = SshSigVerifier.Verify(
            sshSigPem: request.Signature,
            challenge: request.Challenge,
            expectedKeyType: key.KeyType,
            expectedKeyDataBase64: key.KeyData,
            expectedNamespace: challengeService.SshNamespace
        );

        if (sigResult != SshSigVerificationResult.Valid)
        {
            logger.SshSignatureInvalid(
                host: host,
                user: user,
                keyId: keyId.ToString("D"),
                operation: "del",
                result: sigResult
            );

            return Results.BadRequest("SSH signature verification failed.");
        }

        bool removed = await store.RemoveKeyAsync(
            host: host,
            username: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        return removed ? Results.NoContent() : Results.NotFound();
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

        if (!IsValidKeyType(type))
        {
            return false;
        }

        string data = parts[1];

        if (!IsValidBase64(data))
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

    private static bool IsValidBase64(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return Base64Regex().IsMatch(value);
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

    [GeneratedRegex(
        pattern: @"^[A-Za-z0-9+/]+=*$",
        options: RegexOptions.CultureInvariant | RegexOptions.NonBacktracking
    )]
    private static partial Regex Base64Regex();
}
