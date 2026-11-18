using Credfeto.Keys.Server.Cli;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class AdminCommandParserTests : TestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private const string KEY = "ssh-ed25519 AAAA mark@laptop";
    private const string FINGERPRINT = "SHA256:abc";

    [Theory]
    [InlineData("list")]
    [InlineData("add")]
    [InlineData("delete")]
    public static void AdminCommandsAreDetected(string command)
    {
        Assert.True(AdminCommandParser.IsAdminCommand([command]), userMessage: command + " should be an admin command");
    }

    [Theory]
    [InlineData("--self-test")]
    [InlineData("LIST")]
    [InlineData("remove")]
    public static void OtherArgumentsAreNotAdminCommands(string argument)
    {
        Assert.False(
            AdminCommandParser.IsAdminCommand([argument]),
            userMessage: argument + " should not be an admin command"
        );
    }

    [Fact]
    public void NoArgumentsIsNotAnAdminCommand()
    {
        Assert.False(AdminCommandParser.IsAdminCommand([]), userMessage: "No arguments should not be an admin command");
    }

    [Fact]
    public void ListIsParsed()
    {
        AdminCommand command = AssertParsed(["list", "--host", HOST, "--username", USER]);

        Assert.Equal(
            expected: new AdminCommand(
                Operation: AdminOperation.List,
                Host: HOST,
                Username: USER,
                Key: null,
                Fingerprint: null
            ),
            actual: command
        );
    }

    [Fact]
    public void AddIsParsedWithOptionsInAnyOrder()
    {
        AdminCommand command = AssertParsed(["add", "--key", KEY, "--username", USER, "--host", HOST]);

        Assert.Equal(
            expected: new AdminCommand(
                Operation: AdminOperation.Add,
                Host: HOST,
                Username: USER,
                Key: KEY,
                Fingerprint: null
            ),
            actual: command
        );
    }

    [Fact]
    public void DeleteByKeyIsParsed()
    {
        AdminCommand command = AssertParsed(["delete", "--host", HOST, "--username", USER, "--key", KEY]);

        Assert.Equal(
            expected: new AdminCommand(
                Operation: AdminOperation.Delete,
                Host: HOST,
                Username: USER,
                Key: KEY,
                Fingerprint: null
            ),
            actual: command
        );
    }

    [Fact]
    public void DeleteByFingerprintIsParsed()
    {
        AdminCommand command = AssertParsed([
            "delete",
            "--host",
            HOST,
            "--username",
            USER,
            "--fingerprint",
            FINGERPRINT,
        ]);

        Assert.Equal(
            expected: new AdminCommand(
                Operation: AdminOperation.Delete,
                Host: HOST,
                Username: USER,
                Key: null,
                Fingerprint: FINGERPRINT
            ),
            actual: command
        );
    }

    [Fact]
    public void NoArgumentsIsRejected()
    {
        AssertRejected(args: [], expectedError: "Expected a command: add, delete or list");
    }

    [Fact]
    public void UnknownCommandIsRejected()
    {
        AssertRejected(
            args: ["remove", "--host", HOST, "--username", USER],
            expectedError: "Expected a command: add, delete or list"
        );
    }

    [Fact]
    public void MissingHostIsRejected()
    {
        AssertRejected(args: ["list", "--username", USER], expectedError: "Missing required option --host");
    }

    [Fact]
    public void BlankHostIsRejected()
    {
        AssertRejected(
            args: ["list", "--host", " ", "--username", USER],
            expectedError: "Missing required option --host"
        );
    }

    [Fact]
    public void MissingUsernameIsRejected()
    {
        AssertRejected(args: ["list", "--host", HOST], expectedError: "Missing required option --username");
    }

    [Fact]
    public void UnknownOptionIsRejected()
    {
        AssertRejected(
            args: ["list", "--host", HOST, "--username", USER, "--user", USER],
            expectedError: "Unknown option: --user"
        );
    }

    [Fact]
    public void OptionWithoutValueIsRejected()
    {
        AssertRejected(args: ["list", "--username", USER, "--host"], expectedError: "Missing value for --host");
    }

    [Fact]
    public void RepeatedOptionIsRejected()
    {
        AssertRejected(
            args: ["list", "--host", HOST, "--host", "other.example.com", "--username", USER],
            expectedError: "Option --host given more than once"
        );
    }

    [Theory]
    [InlineData("--key", KEY)]
    [InlineData("--fingerprint", FINGERPRINT)]
    public static void ListWithKeyOptionIsRejected(string option, string value)
    {
        AssertRejected(
            args: ["list", "--host", HOST, "--username", USER, option, value],
            expectedError: "list does not take --key or --fingerprint"
        );
    }

    [Fact]
    public void AddWithoutKeyIsRejected()
    {
        AssertRejected(
            args: ["add", "--host", HOST, "--username", USER],
            expectedError: "add requires --key and no --fingerprint"
        );
    }

    [Fact]
    public void AddWithFingerprintIsRejected()
    {
        AssertRejected(
            args: ["add", "--host", HOST, "--username", USER, "--key", KEY, "--fingerprint", FINGERPRINT],
            expectedError: "add requires --key and no --fingerprint"
        );
    }

    [Fact]
    public void DeleteWithoutKeyOrFingerprintIsRejected()
    {
        AssertRejected(
            args: ["delete", "--host", HOST, "--username", USER],
            expectedError: "delete requires exactly one of --key or --fingerprint"
        );
    }

    [Fact]
    public void DeleteWithKeyAndFingerprintIsRejected()
    {
        AssertRejected(
            args: ["delete", "--host", HOST, "--username", USER, "--key", KEY, "--fingerprint", FINGERPRINT],
            expectedError: "delete requires exactly one of --key or --fingerprint"
        );
    }

    private static AdminCommand AssertParsed(string[] args)
    {
        bool parsed = AdminCommandParser.TryParse(args: args, out AdminCommand? command, out string? error);

        Assert.True(condition: parsed, userMessage: "Expected the arguments to parse but got: " + error);
        Assert.Null(error);
        Assert.NotNull(command);

        return command;
    }

    private static void AssertRejected(string[] args, string expectedError)
    {
        bool parsed = AdminCommandParser.TryParse(args: args, out AdminCommand? command, out string? error);

        Assert.False(condition: parsed, userMessage: "Expected the arguments to be rejected");
        Assert.Null(command);
        Assert.Equal(expected: expectedError, actual: error);
    }
}
