using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.Server.Helpers;
using Credfeto.Keys.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Credfeto.Keys.Server.Cli;

public static class AdminCommandRunner
{
    public static ValueTask<int> RunAsync(IReadOnlyList<string> args)
    {
        return RunAsync(
            args: args,
            configurationOverrides: [],
            fileOwnership: new UnixFileOwnership(),
            output: Console.Out,
            error: Console.Error,
            cancellationToken: CancellationToken.None
        );
    }

    public static async ValueTask<int> RunAsync(
        IReadOnlyList<string> args,
        IEnumerable<KeyValuePair<string, string?>> configurationOverrides,
        IFileOwnership fileOwnership,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        if (!AdminCommandParser.TryParse(args: args, out AdminCommand? command, out string? usageError))
        {
            await WriteLineAsync(writer: error, text: usageError, cancellationToken: cancellationToken);
            await WriteLineAsync(writer: error, text: AdminCommandParser.Usage, cancellationToken: cancellationToken);

            return (int)AdminExitCode.Usage;
        }

        try
        {
            await using (
                WebApplication app = ServerStartup.CreateApp(
                    args: [],
                    configurationOverrides: BuildConfiguration(configurationOverrides)
                )
            )
            {
                AdminExitCode exitCode = await ExecuteAsync(
                    command: command,
                    services: app.Services,
                    fileOwnership: fileOwnership,
                    output: output,
                    error: error,
                    cancellationToken: cancellationToken
                );

                return (int)exitCode;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            await WriteLineAsync(
                writer: error,
                text: $"Key store access failed: {exception.Message}",
                cancellationToken: cancellationToken
            );

            return (int)AdminExitCode.IoError;
        }
    }

    // The admin commands never issue or verify challenges, so the challenge service only needs a syntactically
    // valid secret to be constructed; values supplied by the caller still take precedence.
    private static IEnumerable<KeyValuePair<string, string?>> BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> configurationOverrides
    )
    {
        return
        [
            new(key: "Challenge:HmacSecret", value: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            .. configurationOverrides,
        ];
    }

    private static async ValueTask<AdminExitCode> ExecuteAsync(
        AdminCommand command,
        IServiceProvider services,
        IFileOwnership fileOwnership,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        string basePath = services.GetRequiredService<IOptions<FileSystemKeyStoreOptions>>().Value.BasePath;

        if (!IsUsableBasePath(basePath: basePath, fileOwnership: fileOwnership))
        {
            await WriteLineAsync(
                writer: error,
                text: $"Key store '{basePath}' does not exist or is not owned by the current user. "
                    + "Run this command inside the server container as the server user, for example: "
                    + "docker compose exec <service> /usr/src/app/Credfeto.Keys.Server list --host <host> --username <username>",
                cancellationToken: cancellationToken
            );

            return AdminExitCode.IoError;
        }

        IKeyManagementService keyManagementService = services.GetRequiredService<IKeyManagementService>();

        AdminKeyResult result = await RunOperationAsync(
            command: command,
            keyManagementService: keyManagementService,
            cancellationToken: cancellationToken
        );

        return await ReportAsync(
            command: command,
            result: result,
            output: output,
            error: error,
            cancellationToken: cancellationToken
        );
    }

    private static bool IsUsableBasePath(string basePath, IFileOwnership fileOwnership)
    {
        return !string.IsNullOrWhiteSpace(basePath)
            && Directory.Exists(basePath)
            && fileOwnership.IsOwnedByEffectiveUser(basePath);
    }

    private static ValueTask<AdminKeyResult> RunOperationAsync(
        AdminCommand command,
        IKeyManagementService keyManagementService,
        CancellationToken cancellationToken
    )
    {
        return command switch
        {
            { Operation: AdminOperation.Add, Key: { } key } => keyManagementService.AdminAddKeyAsync(
                host: command.Host,
                user: command.Username,
                keyLine: key,
                cancellationToken: cancellationToken
            ),
            { Operation: AdminOperation.Delete, Key: { } key } => keyManagementService.AdminDeleteKeyAsync(
                host: command.Host,
                user: command.Username,
                keyLine: key,
                cancellationToken: cancellationToken
            ),
            { Operation: AdminOperation.Delete, Fingerprint: { } fingerprint } =>
                keyManagementService.AdminDeleteKeyByFingerprintAsync(
                    host: command.Host,
                    user: command.Username,
                    fingerprint: fingerprint,
                    cancellationToken: cancellationToken
                ),
            _ => keyManagementService.AdminListKeysAsync(
                host: command.Host,
                user: command.Username,
                cancellationToken: cancellationToken
            ),
        };
    }

    private static async ValueTask<AdminExitCode> ReportAsync(
        AdminCommand command,
        AdminKeyResult result,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        switch (result.Status)
        {
            case KeyManagementStatus.Success:
                await ReportSuccessAsync(
                    command: command,
                    keys: result.Keys,
                    output: output,
                    cancellationToken: cancellationToken
                );

                return AdminExitCode.Success;
            case KeyManagementStatus.AlreadyExists:
                await WriteKeysAsync(
                    prefix: "Already present, nothing changed: ",
                    keys: result.Keys,
                    output: output,
                    cancellationToken: cancellationToken
                );

                return AdminExitCode.Success;
            default:
                await WriteLineAsync(
                    writer: error,
                    text: DescribeFailure(command: command, status: result.Status),
                    cancellationToken: cancellationToken
                );

                return AdminExitCode.Failed;
        }
    }

    private static ValueTask ReportSuccessAsync(
        AdminCommand command,
        IReadOnlyList<AdminKeyEntry> keys,
        TextWriter output,
        CancellationToken cancellationToken
    )
    {
        return command.Operation switch
        {
            AdminOperation.Add => WriteKeysAsync(
                prefix: "Added: ",
                keys: keys,
                output: output,
                cancellationToken: cancellationToken
            ),
            AdminOperation.Delete => WriteKeysAsync(
                prefix: "Deleted: ",
                keys: keys,
                output: output,
                cancellationToken: cancellationToken
            ),
            _ when keys.Count == 0 => WriteLineAsync(
                writer: output,
                text: $"No keys for {command.Username} on {command.Host}",
                cancellationToken: cancellationToken
            ),
            _ => WriteKeysAsync(prefix: string.Empty, keys: keys, output: output, cancellationToken: cancellationToken),
        };
    }

    private static async ValueTask WriteKeysAsync(
        string prefix,
        IReadOnlyList<AdminKeyEntry> keys,
        TextWriter output,
        CancellationToken cancellationToken
    )
    {
        foreach (AdminKeyEntry key in keys)
        {
            await WriteLineAsync(
                writer: output,
                text: $"{prefix}{key.Fingerprint} {key.KeyType} {key.Comment}".TrimEnd(),
                cancellationToken: cancellationToken
            );
        }
    }

    private static async ValueTask WriteLineAsync(TextWriter writer, string text, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(buffer: text.AsMemory(), cancellationToken: cancellationToken);
    }

    private static string DescribeFailure(AdminCommand command, KeyManagementStatus status)
    {
        return status switch
        {
            KeyManagementStatus.NotFound => $"No matching key for {command.Username} on {command.Host}",
            KeyManagementStatus.InvalidKey when command.Fingerprint is not null =>
                "Invalid fingerprint: expected SHA256:<base64>, as printed by ssh-keygen -lf",
            KeyManagementStatus.InvalidKey =>
                "Invalid key: expected an ssh-ed25519 or sk-ssh-ed25519@openssh.com public key line",
            _ => "Invalid host or username",
        };
    }
}
