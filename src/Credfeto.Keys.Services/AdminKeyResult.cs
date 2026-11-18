using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Keys.Services;

[DebuggerDisplay("{Status}: {Keys.Count}")]
public sealed record AdminKeyResult(KeyManagementStatus Status, IReadOnlyList<AdminKeyEntry> Keys)
{
    public static AdminKeyResult Failed(KeyManagementStatus status)
    {
        return new(Status: status, Keys: []);
    }

    public static AdminKeyResult Completed(KeyManagementStatus status, IReadOnlyList<AdminKeyEntry> keys)
    {
        return new(Status: status, Keys: keys);
    }
}
