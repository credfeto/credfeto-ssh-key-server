using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Keys.Services;

public interface IKeyManagementService
{
    ValueTask<KeyListResult> GetKeysAsync(string host, string user, CancellationToken cancellationToken);

    KeyChallengeResult GenerateAddChallenge(string host, string user);

    ValueTask<AddKeyResult> AddKeyAsync(
        string host,
        string user,
        string key,
        string challenge,
        string signature,
        CancellationToken cancellationToken
    );

    ValueTask<KeyChallengeResult> GenerateDeleteChallengeAsync(
        string host,
        string user,
        Guid keyId,
        CancellationToken cancellationToken
    );

    ValueTask<KeyManagementStatus> DeleteKeyAsync(
        string host,
        string user,
        Guid keyId,
        string challenge,
        string signature,
        CancellationToken cancellationToken
    );
}
