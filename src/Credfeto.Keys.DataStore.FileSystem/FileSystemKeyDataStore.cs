using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.FileSystem.Config;
using Credfeto.Keys.DataStore.FileSystem.Json;
using Credfeto.Keys.DataStore.FileSystem.LoggingExtensions;
using Credfeto.Keys.DataStore.Interfaces;
using Credfeto.Keys.DataStore.Interfaces.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NonBlocking;

namespace Credfeto.Keys.DataStore.FileSystem;

public sealed class FileSystemKeyDataStore : ISshKeyDataStore
{
    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    private readonly ILogger<FileSystemKeyDataStore> _logger;
    private readonly TimeProvider _timeProvider;

    public FileSystemKeyDataStore(
        IOptions<FileSystemKeyStoreOptions> options,
        TimeProvider timeProvider,
        ILogger<FileSystemKeyDataStore> logger
    )
    {
        this._logger = logger;
        this._timeProvider = timeProvider;
        this._basePath = options.Value.BasePath;
        this._locks = new(comparer: StringComparer.Ordinal);

        Directory.CreateDirectory(this._basePath);
    }

    public async ValueTask<IReadOnlyList<SshPublicKey>> GetKeysAsync(
        string host,
        string username,
        CancellationToken cancellationToken
    )
    {
        string filePath = this.BuildKeyFilePath(host: host, username: username);

        this._logger.LoadingKeys(filePath);

        StoredKeyFile? stored = await this.ReadKeyFileAsync(filePath: filePath, cancellationToken: cancellationToken);

        if (stored is null)
        {
            return [];
        }

        return [.. stored.Keys.Select(ToPublicKey)];
    }

    public async ValueTask<SshPublicKey> AddKeyAsync(
        string host,
        string username,
        string keyType,
        string keyData,
        string comment,
        CancellationToken cancellationToken
    )
    {
        string filePath = this.BuildKeyFilePath(host: host, username: username);
        string lockKey = BuildLockKey(host: host, username: username);
        SemaphoreSlim semaphore = this._locks.GetOrAdd(
            key: lockKey,
            value: new SemaphoreSlim(initialCount: 1, maxCount: 1)
        );

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            StoredKeyFile stored = await this.ReadOrCreateKeyFileAsync(
                filePath: filePath,
                cancellationToken: cancellationToken
            );

            SshPublicKey key = new()
            {
                KeyId = Guid.NewGuid(),
                KeyType = keyType,
                KeyData = keyData,
                Comment = comment,
                AddedAt = this._timeProvider.GetUtcNow(),
            };

            stored.Keys.Add(ToStoredKey(key));

            await this.WriteKeyFileAsync(filePath: filePath, stored: stored, cancellationToken: cancellationToken);

            this._logger.AddedKey(keyId: key.KeyId, username: username, host: host);

            return key;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async ValueTask<bool> RemoveKeyAsync(
        string host,
        string username,
        Guid keyId,
        CancellationToken cancellationToken
    )
    {
        string filePath = this.BuildKeyFilePath(host: host, username: username);
        string lockKey = BuildLockKey(host: host, username: username);
        SemaphoreSlim semaphore = this._locks.GetOrAdd(
            key: lockKey,
            value: new SemaphoreSlim(initialCount: 1, maxCount: 1)
        );

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            StoredKeyFile? stored = await this.ReadKeyFileAsync(
                filePath: filePath,
                cancellationToken: cancellationToken
            );

            if (stored is null)
            {
                this._logger.KeyNotFound(keyId: keyId, username: username, host: host);

                return false;
            }

            int removed = stored.Keys.RemoveAll(k => k.KeyId == keyId);

            if (removed == 0)
            {
                this._logger.KeyNotFound(keyId: keyId, username: username, host: host);

                return false;
            }

            await this.WriteKeyFileAsync(filePath: filePath, stored: stored, cancellationToken: cancellationToken);

            this._logger.RemovedKey(keyId: keyId, username: username, host: host);

            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async ValueTask<StoredKeyFile> ReadOrCreateKeyFileAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        StoredKeyFile? existing = await this.ReadKeyFileAsync(filePath: filePath, cancellationToken: cancellationToken);

        return existing ?? new StoredKeyFile { Keys = [] };
    }

    private async ValueTask<StoredKeyFile?> ReadKeyFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);

            return await JsonSerializer.DeserializeAsync(
                utf8Json: stream,
                jsonTypeInfo: KeyFileJsonContext.Default.StoredKeyFile,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            this._logger.FailedToReadKeyFile(filePath: filePath, message: exception.Message, exception: exception);

            return null;
        }
    }

    private async ValueTask WriteKeyFileAsync(
        string filePath,
        StoredKeyFile stored,
        CancellationToken cancellationToken
    )
    {
        this._logger.SavingKeys(filePath);

        string? directory = Path.GetDirectoryName(filePath);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = filePath + ".tmp";

        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(
                    utf8Json: stream,
                    value: stored,
                    jsonTypeInfo: KeyFileJsonContext.Default.StoredKeyFile,
                    cancellationToken: cancellationToken
                );
            }

            File.Move(sourceFileName: tempPath, destFileName: filePath, overwrite: true);
        }
        catch (Exception exception)
        {
            this._logger.FailedToSaveKeyFile(filePath: filePath, message: exception.Message, exception: exception);

            throw;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private string BuildKeyFilePath(string host, string username)
    {
        return Path.Combine(path1: this._basePath, path2: host, path3: username + ".json");
    }

    private static string BuildLockKey(string host, string username)
    {
        return host + "/" + username;
    }

    private static SshPublicKey ToPublicKey(StoredSshKey stored)
    {
        return new SshPublicKey
        {
            KeyId = stored.KeyId,
            KeyType = stored.KeyType,
            KeyData = stored.KeyData,
            Comment = stored.Comment,
            AddedAt = stored.AddedAt,
        };
    }

    private static StoredSshKey ToStoredKey(SshPublicKey key)
    {
        return new StoredSshKey
        {
            KeyId = key.KeyId,
            KeyType = key.KeyType,
            KeyData = key.KeyData,
            Comment = key.Comment,
            AddedAt = key.AddedAt,
        };
    }
}
