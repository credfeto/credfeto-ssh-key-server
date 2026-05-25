using System.Text.Json.Serialization;

namespace Credfeto.Keys.DataStore.FileSystem.Json;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StoredKeyFile))]
internal sealed partial class KeyFileJsonContext : JsonSerializerContext { }
