using System;
using System.Diagnostics;

namespace Credfeto.Keys.Services;

[DebuggerDisplay("{Status}: {KeyId}")]
public sealed record AddKeyResult(KeyManagementStatus Status, Guid KeyId)
{
    public static AddKeyResult Failed(KeyManagementStatus status)
    {
        return new(Status: status, KeyId: Guid.Empty);
    }

    public static AddKeyResult Created(in Guid keyId)
    {
        return new(Status: KeyManagementStatus.Success, KeyId: keyId);
    }
}
