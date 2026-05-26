using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.DataStore.Interfaces.Models;

namespace Credfeto.Keys.DataStore.Interfaces;

public interface ISshKeyDataStore
{
    ValueTask<IReadOnlyList<SshPublicKey>> GetKeysAsync(
        string host,
        string username,
        CancellationToken cancellationToken
    );

    ValueTask<SshPublicKey> AddKeyAsync(
        string host,
        string username,
        string keyType,
        string keyData,
        string comment,
        CancellationToken cancellationToken
    );

    ValueTask<SshPublicKey?> GetKeyByIdAsync(
        string host,
        string username,
        Guid keyId,
        CancellationToken cancellationToken
    );

    ValueTask<bool> RemoveKeyAsync(string host, string username, Guid keyId, CancellationToken cancellationToken);
}
