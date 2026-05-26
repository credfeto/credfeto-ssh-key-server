using System;
using System.Diagnostics;

namespace Credfeto.Keys.DataStore.Interfaces.Models;

[DebuggerDisplay("KeyId: {KeyId}, Type: {KeyType}, Comment: {Comment}")]
public sealed record SshPublicKey
{
    public required Guid KeyId { get; init; }

    public required string KeyType { get; init; }

    public required string KeyData { get; init; }

    public required string Comment { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}
