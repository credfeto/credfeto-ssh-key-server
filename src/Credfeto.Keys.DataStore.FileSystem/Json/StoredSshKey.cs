using System;
using System.Text.Json.Serialization;

namespace Credfeto.Keys.DataStore.FileSystem.Json;

internal sealed class StoredSshKey
{
    [JsonPropertyName("keyId")]
    public required Guid KeyId { get; init; }

    [JsonPropertyName("keyType")]
    public required string KeyType { get; init; }

    [JsonPropertyName("keyData")]
    public required string KeyData { get; init; }

    [JsonPropertyName("comment")]
    public required string Comment { get; init; }

    [JsonPropertyName("addedAt")]
    public required DateTimeOffset AddedAt { get; init; }
}
