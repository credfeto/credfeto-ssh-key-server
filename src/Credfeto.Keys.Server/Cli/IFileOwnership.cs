namespace Credfeto.Keys.Server.Cli;

public interface IFileOwnership
{
    bool IsOwnedByEffectiveUser(string path);
}
