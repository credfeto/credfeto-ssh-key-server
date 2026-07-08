using System.Diagnostics;

namespace Credfeto.Keys.Server.Config;

[DebuggerDisplay("CertificatePath: {CertificatePath}")]
public sealed class HttpsOptions
{
    public string? CertificatePath { get; init; }

    public string? CertificatePassword { get; init; }
}
