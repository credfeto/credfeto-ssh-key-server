using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Keys.DataStore.FileSystem.Tests;

public sealed class FileSystemKeyDataStoreTests : LoggingFolderCleanupTestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";

    public FileSystemKeyDataStoreTests(ITestOutputHelper output)
        : base(output) { }

    private FileSystemKeyDataStore CreateStore(string basePath)
    {
        IOptions<FileSystemKeyStoreOptions> options = Options.Create(
            new FileSystemKeyStoreOptions { BasePath = basePath }
        );

        return new FileSystemKeyDataStore(
            options: options,
            timeProvider: new FakeTimeProvider(),
            logger: this.GetTypedLogger<FileSystemKeyDataStore>()
        );
    }

    private static string KeyFileDirectory(string basePath)
    {
        return Path.Combine(path1: basePath, path2: HOST);
    }

    private static string KeyFilePath(string basePath)
    {
        return Path.Combine(path1: KeyFileDirectory(basePath), path2: USER + ".json");
    }

    [Fact]
    public async Task AddKeyCreatesFileWhenNoneExistsAsync()
    {
        string basePath = this.CreateFolderInTempFolder(Guid.NewGuid().ToString());
        ISshKeyDataStore store = this.CreateStore(basePath);

        SshPublicKey key = await store.AddKeyAsync(
            host: HOST,
            username: USER,
            keyType: "ssh-ed25519",
            keyData: "AAAA",
            comment: "test",
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: File.Exists(KeyFilePath(basePath)), userMessage: "Key file should have been created");

        IReadOnlyList<SshPublicKey> keys = await store.GetKeysAsync(
            host: HOST,
            username: USER,
            cancellationToken: this.CancellationToken()
        );
        SshPublicKey stored = Assert.Single(keys);
        Assert.Equal(expected: key.KeyId, actual: stored.KeyId);
    }

    [Fact]
    public async Task AddKeyThrowsAndDoesNotOverwriteFileWhenExistingFileIsCorruptAsync()
    {
        string basePath = this.CreateFolderInTempFolder(Guid.NewGuid().ToString());
        string filePath = KeyFilePath(basePath);
        this.EnsureDirectoryExists(KeyFileDirectory(basePath));

        const string corruptContent = "{ not valid json";
        await File.WriteAllTextAsync(
            path: filePath,
            contents: corruptContent,
            encoding: Encoding.UTF8,
            cancellationToken: this.CancellationToken()
        );

        ISshKeyDataStore store = this.CreateStore(basePath);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store
                .AddKeyAsync(
                    host: HOST,
                    username: USER,
                    keyType: "ssh-ed25519",
                    keyData: "AAAA",
                    comment: "test",
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );

        string contentAfter = await File.ReadAllTextAsync(
            path: filePath,
            encoding: Encoding.UTF8,
            cancellationToken: this.CancellationToken()
        );
        Assert.Equal(expected: corruptContent, actual: contentAfter);
    }

    [Fact]
    public async Task GetKeysThrowsWhenExistingFileIsCorruptAsync()
    {
        string basePath = this.CreateFolderInTempFolder(Guid.NewGuid().ToString());
        string filePath = KeyFilePath(basePath);
        this.EnsureDirectoryExists(KeyFileDirectory(basePath));

        await File.WriteAllTextAsync(
            path: filePath,
            contents: "{ not valid json",
            encoding: Encoding.UTF8,
            cancellationToken: this.CancellationToken()
        );

        ISshKeyDataStore store = this.CreateStore(basePath);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.GetKeysAsync(host: HOST, username: USER, cancellationToken: this.CancellationToken()).AsTask()
        );
    }
}
