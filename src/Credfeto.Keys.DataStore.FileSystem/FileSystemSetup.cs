using Credfeto.Keys.DataStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Keys.DataStore.FileSystem;

public static class FileSystemSetup
{
    public static IServiceCollection AddFileSystemKeyStorage(this IServiceCollection services)
    {
        return services.AddSingleton<ISshKeyDataStore, FileSystemKeyDataStore>();
    }
}
