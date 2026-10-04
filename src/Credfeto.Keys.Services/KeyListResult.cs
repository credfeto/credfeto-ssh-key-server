using System.Diagnostics;

namespace Credfeto.Keys.Services;

[DebuggerDisplay("{Status}")]
public sealed record KeyListResult(KeyManagementStatus Status, string AuthorizedKeys)
{
    public static KeyListResult InvalidRequest { get; } =
        new(Status: KeyManagementStatus.InvalidRequest, AuthorizedKeys: string.Empty);

    public static KeyListResult Success(string authorizedKeys)
    {
        return new(Status: KeyManagementStatus.Success, AuthorizedKeys: authorizedKeys);
    }
}
