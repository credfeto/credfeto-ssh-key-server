using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.Keys.DataStore.FileSystem.Json;

internal sealed class StoredKeyFile
{
    [JsonPropertyName("keys")]
    public required List<StoredSshKey> Keys { get; init; }
}
