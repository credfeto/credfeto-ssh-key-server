using System.Text.Json.Serialization;
using Credfeto.Keys.Server.Models;

namespace Credfeto.Keys.Server.Json;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(PongDto))]
[JsonSerializable(typeof(AddKeyResponse))]
internal sealed partial class AppJsonContexts : JsonSerializerContext { }
