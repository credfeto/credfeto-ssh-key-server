using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.Server.Models;
using Credfeto.Keys.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Credfeto.Keys.Server.Helpers;

public static partial class Endpoints
{
    private const string INVALID_CHALLENGE_MESSAGE = "Invalid or expired challenge.";
    private const string INVALID_SIGNATURE_MESSAGE = "SSH signature verification failed.";

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
        IKeyManagementService keyManagementService,
        CancellationToken cancellationToken
    )
    {
        KeyListResult result = await keyManagementService.GetKeysAsync(
            host: host,
            user: user,
            cancellationToken: cancellationToken
        );

        if (result.Status != KeyManagementStatus.Success)
        {
            return Results.BadRequest();
        }

        return string.IsNullOrEmpty(result.AuthorizedKeys)
            ? Results.Ok(string.Empty)
            : Results.Text(result.AuthorizedKeys, contentType: "text/plain");
    }

    private static ValueTask<IResult> GetAddChallengeAsync(
        string host,
        string user,
        IKeyManagementService keyManagementService
    )
    {
        KeyChallengeResult result = keyManagementService.GenerateAddChallenge(host: host, user: user);

        return ValueTask.FromResult(ToChallengeResult(result));
    }

    private static async ValueTask<IResult> AddKeyAsync(
        string host,
        string user,
        [FromBody] AddKeyRequest request,
        IKeyManagementService keyManagementService,
        CancellationToken cancellationToken
    )
    {
        AddKeyResult result = await keyManagementService.AddKeyAsync(
            host: host,
            user: user,
            key: request.Key,
            challenge: request.Challenge,
            signature: request.Signature,
            cancellationToken: cancellationToken
        );

        return result.Status switch
        {
            KeyManagementStatus.Success => Results.Created(uri: (string?)null, value: new AddKeyResponse(result.KeyId)),
            KeyManagementStatus.InvalidChallenge => Results.BadRequest(INVALID_CHALLENGE_MESSAGE),
            KeyManagementStatus.InvalidKey => Results.BadRequest("Invalid SSH public key format."),
            KeyManagementStatus.InvalidSignature => Results.BadRequest(INVALID_SIGNATURE_MESSAGE),
            _ => Results.BadRequest("Invalid host or username."),
        };
    }

    private static async ValueTask<IResult> GetDeleteChallengeAsync(
        string host,
        string user,
        Guid keyId,
        IKeyManagementService keyManagementService,
        CancellationToken cancellationToken
    )
    {
        KeyChallengeResult result = await keyManagementService.GenerateDeleteChallengeAsync(
            host: host,
            user: user,
            keyId: keyId,
            cancellationToken: cancellationToken
        );

        return ToChallengeResult(result);
    }

    private static async ValueTask<IResult> DeleteKeyAsync(
        string host,
        string user,
        Guid keyId,
        [FromBody] DeleteKeyRequest request,
        IKeyManagementService keyManagementService,
        CancellationToken cancellationToken
    )
    {
        KeyManagementStatus status = await keyManagementService.DeleteKeyAsync(
            host: host,
            user: user,
            keyId: keyId,
            challenge: request.Challenge,
            signature: request.Signature,
            cancellationToken: cancellationToken
        );

        return status switch
        {
            KeyManagementStatus.Success => Results.NoContent(),
            KeyManagementStatus.InvalidChallenge => Results.BadRequest(INVALID_CHALLENGE_MESSAGE),
            KeyManagementStatus.InvalidSignature => Results.BadRequest(INVALID_SIGNATURE_MESSAGE),
            KeyManagementStatus.NotFound => Results.NotFound(),
            _ => Results.BadRequest(),
        };
    }

    private static IResult ToChallengeResult(KeyChallengeResult result)
    {
        return result switch
        {
            { Status: KeyManagementStatus.Success, Challenge: { } challenge } => Results.Ok(
                new ChallengeDto(
                    Challenge: challenge.Token,
                    Namespace: challenge.Namespace,
                    ValidUntil: challenge.ValidUntil
                )
            ),
            { Status: KeyManagementStatus.NotFound } => Results.NotFound(),
            _ => Results.BadRequest(),
        };
    }
}
