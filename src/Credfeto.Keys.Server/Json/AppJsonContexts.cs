using System.Text.Json.Serialization;
using Credfeto.Keys.Server.Models;

namespace Credfeto.Keys.Server.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PongDto))]
[JsonSerializable(typeof(AddKeyResponse))]
[JsonSerializable(typeof(ChallengeDto))]
[JsonSerializable(typeof(AddKeyRequest))]
[JsonSerializable(typeof(DeleteKeyRequest))]
internal sealed partial class AppJsonContexts : JsonSerializerContext { }
