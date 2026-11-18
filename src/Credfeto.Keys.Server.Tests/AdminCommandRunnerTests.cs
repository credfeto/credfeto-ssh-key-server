using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Keys.Server.Cli;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class AdminCommandRunnerTests : LoggingFolderCleanupTestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private const string KEY_DATA = "AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl";
    private const string KEY_LINE = "ssh-ed25519 " + KEY_DATA + " mark@laptop";
    private const string FINGERPRINT = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";
    private const string ENTRY = FINGERPRINT + " ssh-ed25519 mark@laptop";

    private readonly string _basePath;
    private readonly IFileOwnership _fileOwnership;

    public AdminCommandRunnerTests(ITestOutputHelper output)
        : base(output)
    {
        this._basePath = this.CreateFolderInTempFolder(Guid.NewGuid().ToString());
        this._fileOwnership = GetSubstitute<IFileOwnership>();
        this.MockFileOwnershipIsOwnedByEffectiveUser(true);
    }

    [Fact]
    public async Task AddStoresKeyAndReportsItAsync()
    {
        AdminRun run = await this.RunAsync("add", "--host", HOST, "--username", USER, "--key", KEY_LINE);

        AssertSucceeded(run: run, expectedOutput: "Added: " + ENTRY);
        Assert.Equal(expected: ENTRY, actual: (await this.ListAsync()).Output);
    }

    [Fact]
    public async Task AddOfExistingKeyIsNoOpAsync()
    {
        await this.AddKeyAsync();

        AdminRun run = await this.RunAsync(
            "add",
            "--host",
            HOST,
            "--username",
            USER,
            "--key",
            "ssh-ed25519 " + KEY_DATA + " other comment"
        );

        AssertSucceeded(run: run, expectedOutput: "Already present, nothing changed: " + ENTRY);
        Assert.Equal(expected: ENTRY, actual: (await this.ListAsync()).Output);
    }

    [Fact]
    public async Task ListWithNoKeysSaysSoAsync()
    {
        AdminRun run = await this.ListAsync();

        AssertSucceeded(run: run, expectedOutput: "No keys for " + USER + " on " + HOST);
    }

    [Fact]
    public async Task DeleteByKeyRemovesKeyAsync()
    {
        await this.AddKeyAsync();

        AdminRun run = await this.RunAsync(
            "delete",
            "--host",
            HOST,
            "--username",
            USER,
            "--key",
            "ssh-ed25519 " + KEY_DATA
        );

        AssertSucceeded(run: run, expectedOutput: "Deleted: " + ENTRY);
        Assert.Equal(expected: "No keys for " + USER + " on " + HOST, actual: (await this.ListAsync()).Output);
    }

    [Fact]
    public async Task DeleteByFingerprintRemovesKeyAsync()
    {
        await this.AddKeyAsync();

        AdminRun run = await this.RunAsync("delete", "--host", HOST, "--username", USER, "--fingerprint", FINGERPRINT);

        AssertSucceeded(run: run, expectedOutput: "Deleted: " + ENTRY);
    }

    [Fact]
    public async Task DeleteOfMissingKeyFailsAsync()
    {
        AdminRun run = await this.RunAsync("delete", "--host", HOST, "--username", USER, "--fingerprint", FINGERPRINT);

        AssertFailed(
            run: run,
            expectedExitCode: AdminExitCode.Failed,
            expectedError: "No matching key for " + USER + " on " + HOST
        );
    }

    [Fact]
    public async Task InvalidKeyFailsAsync()
    {
        AdminRun run = await this.RunAsync("add", "--host", HOST, "--username", USER, "--key", "ssh-rsa AAAA");

        AssertFailed(
            run: run,
            expectedExitCode: AdminExitCode.Failed,
            expectedError: "Invalid key: expected an ssh-ed25519 or sk-ssh-ed25519@openssh.com public key line"
        );
    }

    [Fact]
    public async Task InvalidFingerprintFailsAsync()
    {
        AdminRun run = await this.RunAsync("delete", "--host", HOST, "--username", USER, "--fingerprint", "MD5:00");

        AssertFailed(
            run: run,
            expectedExitCode: AdminExitCode.Failed,
            expectedError: "Invalid fingerprint: expected SHA256:<base64>, as printed by ssh-keygen -lf"
        );
    }

    [Fact]
    public async Task InvalidHostFailsAsync()
    {
        AdminRun run = await this.RunAsync("list", "--host", "bad host!", "--username", USER);

        AssertFailed(run: run, expectedExitCode: AdminExitCode.Failed, expectedError: "Invalid host or username");
    }

    [Fact]
    public async Task MissingHostIsUsageErrorAsync()
    {
        AdminRun run = await this.RunAsync("list", "--username", USER);

        Assert.Equal(expected: AdminExitCode.Usage, actual: run.ExitCode);
        Assert.Empty(run.Output);
        Assert.StartsWith(
            expectedStartString: "Missing required option --host",
            actualString: run.Error,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: AdminCommandParser.Usage,
            actualString: run.Error,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task KeyStoreNotOwnedByCurrentUserIsRefusedAsync()
    {
        this.MockFileOwnershipIsOwnedByEffectiveUser(false);

        AdminRun run = await this.ListAsync();

        Assert.Equal(expected: AdminExitCode.IoError, actual: run.ExitCode);
        Assert.Contains(
            expectedSubstring: "docker compose exec",
            actualString: run.Error,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task MissingKeyStoreIsRefusedAsync()
    {
        AdminRun run = await this.RunWithBasePathAsync(
            basePath: Path.Combine(path1: this._basePath, path2: "missing"),
            "list",
            "--host",
            HOST,
            "--username",
            USER
        );

        Assert.Equal(expected: AdminExitCode.IoError, actual: run.ExitCode);
        Assert.Contains(
            expectedSubstring: "is not owned by the current user",
            actualString: run.Error,
            comparisonType: StringComparison.Ordinal
        );
        Assert.False(
            condition: Directory.Exists(Path.Combine(path1: this._basePath, path2: "missing")),
            userMessage: "The refused key store should not have been created"
        );
    }

    [Fact]
    public async Task CorruptKeyFileIsReportedAsIoErrorAsync()
    {
        string hostPath = Path.Combine(path1: this._basePath, path2: HOST);
        this.EnsureDirectoryExists(hostPath);
        await File.WriteAllTextAsync(
            path: Path.Combine(path1: hostPath, path2: USER + ".json"),
            contents: "{ not valid json",
            cancellationToken: this.CancellationToken()
        );

        AdminRun run = await this.ListAsync();

        Assert.Equal(expected: AdminExitCode.IoError, actual: run.ExitCode);
        Assert.StartsWith(
            expectedStartString: "Key store access failed: ",
            actualString: run.Error,
            comparisonType: StringComparison.Ordinal
        );
    }

    private static void AssertSucceeded(AdminRun run, string expectedOutput)
    {
        Assert.Equal(expected: AdminExitCode.Success, actual: run.ExitCode);
        Assert.Equal(expected: expectedOutput, actual: run.Output);
        Assert.Empty(run.Error);
    }

    private static void AssertFailed(AdminRun run, AdminExitCode expectedExitCode, string expectedError)
    {
        Assert.Equal(expected: expectedExitCode, actual: run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal(expected: expectedError, actual: run.Error);
    }

    private async ValueTask AddKeyAsync()
    {
        AdminRun run = await this.RunAsync("add", "--host", HOST, "--username", USER, "--key", KEY_LINE);

        Assert.Equal(expected: AdminExitCode.Success, actual: run.ExitCode);
    }

    private ValueTask<AdminRun> ListAsync()
    {
        return this.RunAsync("list", "--host", HOST, "--username", USER);
    }

    private ValueTask<AdminRun> RunAsync(params string[] args)
    {
        return this.RunWithBasePathAsync(basePath: this._basePath, args: args);
    }

    private async ValueTask<AdminRun> RunWithBasePathAsync(string basePath, params string[] args)
    {
        await using (StringWriter output = new())
        {
            await using (StringWriter error = new())
            {
                int exitCode = await AdminCommandRunner.RunAsync(
                    args: args,
                    configurationOverrides: [new(key: "Keys:BasePath", value: basePath)],
                    fileOwnership: this._fileOwnership,
                    output: output,
                    error: error,
                    cancellationToken: this.CancellationToken()
                );

                return new(
                    ExitCode: (AdminExitCode)exitCode,
                    Output: output.ToString().TrimEnd(),
                    Error: error.ToString().TrimEnd()
                );
            }
        }
    }

    private void MockFileOwnershipIsOwnedByEffectiveUser(bool owned)
    {
        this._fileOwnership.IsOwnedByEffectiveUser(Arg.Any<string>()).Returns(owned);
    }
}
