using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Keys.DataStore.FileSystem.LoggingExtensions;

internal static partial class FileSystemKeyDataStoreLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Loading keys from {filePath}")]
    public static partial void LoadingKeys(this ILogger logger, string filePath);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Saving keys to {filePath}")]
    public static partial void SavingKeys(this ILogger logger, string filePath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to read key file {filePath}: {message}")]
    public static partial void FailedToReadKeyFile(
        this ILogger logger,
        string filePath,
        string message,
        Exception exception
    );

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Failed to save key file {filePath}: {message}")]
    public static partial void FailedToSaveKeyFile(
        this ILogger logger,
        string filePath,
        string message,
        Exception exception
    );

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Added key {keyId} for {username}@{host}")]
    public static partial void AddedKey(this ILogger logger, Guid keyId, string username, string host);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Removed key {keyId} for {username}@{host}")]
    public static partial void RemovedKey(this ILogger logger, Guid keyId, string username, string host);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Key {keyId} not found for {username}@{host}")]
    public static partial void KeyNotFound(this ILogger logger, Guid keyId, string username, string host);
}
