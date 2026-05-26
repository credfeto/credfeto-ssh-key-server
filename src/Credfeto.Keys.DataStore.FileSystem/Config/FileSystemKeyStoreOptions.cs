using System.Diagnostics;

namespace Credfeto.Keys.DataStore.FileSystem.Config;

[DebuggerDisplay("BasePath: {BasePath}")]
public sealed class FileSystemKeyStoreOptions
{
    public required string BasePath { get; init; }
}
