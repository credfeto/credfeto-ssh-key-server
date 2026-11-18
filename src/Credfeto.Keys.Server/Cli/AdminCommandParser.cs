using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Credfeto.Keys.Server.Cli;

public static class AdminCommandParser
{
    private const string HOST_OPTION = "--host";
    private const string USERNAME_OPTION = "--username";
    private const string KEY_OPTION = "--key";
    private const string FINGERPRINT_OPTION = "--fingerprint";

    private static readonly string[] KnownOptions = [HOST_OPTION, USERNAME_OPTION, KEY_OPTION, FINGERPRINT_OPTION];

    public static string Usage { get; } =
        "Usage:\n"
        + "  Credfeto.Keys.Server list --host <host> --username <username>\n"
        + "  Credfeto.Keys.Server add --host <host> --username <username> --key \"<public key line>\"\n"
        + "  Credfeto.Keys.Server delete --host <host> --username <username> --key \"<public key line>\"\n"
        + "  Credfeto.Keys.Server delete --host <host> --username <username> --fingerprint SHA256:<fingerprint>";

    public static bool IsAdminCommand(IReadOnlyList<string> args)
    {
        return args.Count > 0 && TryGetOperation(name: args[0], out _);
    }

    public static bool TryParse(
        IReadOnlyList<string> args,
        [NotNullWhen(true)] out AdminCommand? command,
        [NotNullWhen(false)] out string? error
    )
    {
        command = null;

        if (args.Count == 0 || !TryGetOperation(name: args[0], out AdminOperation operation))
        {
            error = "Expected a command: add, delete or list";

            return false;
        }

        if (
            !TryReadOptions(args: args, out Dictionary<string, string> options, error: out error)
            || !TryGetRequired(options: options, name: HOST_OPTION, out string? host, error: out error)
            || !TryGetRequired(options: options, name: USERNAME_OPTION, out string? username, error: out error)
        )
        {
            return false;
        }

        string? key = options.GetValueOrDefault(KEY_OPTION);
        string? fingerprint = options.GetValueOrDefault(FINGERPRINT_OPTION);

        error = ValidateKeyOptions(operation: operation, key: key, fingerprint: fingerprint);

        if (error is not null)
        {
            return false;
        }

        command = new(Operation: operation, Host: host, Username: username, Key: key, Fingerprint: fingerprint);

        return true;
    }

    private static bool TryGetOperation(string name, out AdminOperation operation)
    {
        switch (name)
        {
            case "list":
                operation = AdminOperation.List;

                return true;
            case "add":
                operation = AdminOperation.Add;

                return true;
            case "delete":
                operation = AdminOperation.Delete;

                return true;
            default:
                operation = default;

                return false;
        }
    }

    private static bool TryReadOptions(
        IReadOnlyList<string> args,
        out Dictionary<string, string> options,
        [NotNullWhen(false)] out string? error
    )
    {
        options = [with(StringComparer.Ordinal)];

        for (int index = 1; index < args.Count; index += 2)
        {
            string name = args[index];

            if (!KnownOptions.Contains(value: name, comparer: StringComparer.Ordinal))
            {
                error = $"Unknown option: {name}";

                return false;
            }

            if (index + 1 >= args.Count)
            {
                error = $"Missing value for {name}";

                return false;
            }

            if (!options.TryAdd(key: name, value: args[index + 1]))
            {
                error = $"Option {name} given more than once";

                return false;
            }
        }

        error = null;

        return true;
    }

    private static bool TryGetRequired(
        Dictionary<string, string> options,
        string name,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(false)] out string? error
    )
    {
        if (options.TryGetValue(key: name, out value) && !string.IsNullOrWhiteSpace(value))
        {
            error = null;

            return true;
        }

        value = null;
        error = $"Missing required option {name}";

        return false;
    }

    private static string? ValidateKeyOptions(AdminOperation operation, string? key, string? fingerprint)
    {
        return operation switch
        {
            AdminOperation.List when key is not null || fingerprint is not null =>
                "list does not take --key or --fingerprint",
            AdminOperation.Add when key is null || fingerprint is not null => "add requires --key and no --fingerprint",
            AdminOperation.Delete when key is null == fingerprint is null =>
                "delete requires exactly one of --key or --fingerprint",
            _ => null,
        };
    }
}
