using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using Credfeto.Keys.Server.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Keys.Server.Helpers;

internal static partial class Endpoints
{
    private static readonly string[] ValidKeyTypes =
    [
        "ssh-rsa",
        "ssh-dss",
        "ssh-ed25519",
        "ecdsa-sha2-nistp256",
        "ecdsa-sha2-nistp384",
        "ecdsa-sha2-nistp521",
        "sk-ssh-ed25519@openssh.com",
        "sk-ecdsa-sha2-nistp256@openssh.com",
    ];

    private static WebApplication ConfigureKeysEndpoints(this WebApplication app)
    {
        Console.WriteLine("Configuring SSH Keys Endpoints");

        app.MapGet(pattern: "/keys/{host}/{user}", handler: GetKeysAsync);
        app.MapPost(pattern: "/keys/{host}/{user}", handler: AddKeyAsync);
        app.MapDelete(pattern: "/keys/{host}/{user}/{keyId}", handler: RemoveKeyAsync);

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

    private static async ValueTask<IResult> AddKeyAsync(
        string host,
        string user,
        HttpRequest request,
        ISshKeyDataStore store,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return Results.BadRequest("Invalid host or username.");
        }

        using StreamReader reader = new(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true
        );
        string keyLine = await reader.ReadToEndAsync(cancellationToken);
        keyLine = keyLine.Trim();

        if (
            !TryParseKeyLine(
                keyLine: keyLine,
                keyType: out string? keyType,
                keyData: out string? keyData,
                comment: out string? comment
            )
        )
        {
            return Results.BadRequest("Invalid SSH public key format.");
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

    private static async ValueTask<IResult> RemoveKeyAsync(
        string host,
        string user,
        Guid keyId,
        ISshKeyDataStore store,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidHost(host) || !IsValidUsername(user))
        {
            return Results.BadRequest();
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
