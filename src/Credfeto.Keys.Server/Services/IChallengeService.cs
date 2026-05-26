using System;

namespace Credfeto.Keys.Server.Services;

public interface IChallengeService
{
    string SshNamespace { get; }

    string GenerateAddChallenge(string host, string user);

    string GenerateDeleteChallenge(string host, string user, Guid keyId);

    ChallengeVerificationResult VerifyAddChallenge(string host, string user, string token);

    ChallengeVerificationResult VerifyDeleteChallenge(string host, string user, Guid keyId, string token);
}
