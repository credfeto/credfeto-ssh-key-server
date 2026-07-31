using System.Diagnostics;

namespace Credfeto.Keys.DataStore.FileSystem.Config;

[DebuggerDisplay("BasePath: {BasePath}")]
public sealed class FileSystemKeyStoreOptions
{
    // Must be a plain settable (not init/required) property: the Configuration
    // Binding Source Generator constructs a default instance and assigns each
    // bound property afterwards, which init/required members reject at compile
    // time — under Native AOT that silently leaves this unset instead of failing
    // the build, so the app starts with an empty BasePath.
    public string BasePath { get; set; } = string.Empty;
}
