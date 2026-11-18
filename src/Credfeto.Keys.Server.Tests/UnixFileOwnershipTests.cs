using System;
using System.IO;
using Credfeto.Keys.Server.Cli;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class UnixFileOwnershipTests : LoggingFolderCleanupTestBase
{
    private readonly IFileOwnership _fileOwnership;

    public UnixFileOwnershipTests(ITestOutputHelper output)
        : base(output)
    {
        this._fileOwnership = new UnixFileOwnership();
    }

    [Fact]
    public void DirectoryCreatedByCurrentUserIsOwned()
    {
        string path = this.CreateFolderInTempFolder(Guid.NewGuid().ToString());

        Assert.True(
            this._fileOwnership.IsOwnedByEffectiveUser(path),
            userMessage: "A directory the test created should be owned by the effective user"
        );
    }

    [Fact]
    public void MissingPathIsNotOwned()
    {
        string path = Path.Combine(path1: this.CreateFolderInTempFolder(Guid.NewGuid().ToString()), path2: "missing");

        Assert.Equal(expected: !OperatingSystem.IsLinux(), actual: this._fileOwnership.IsOwnedByEffectiveUser(path));
    }

    [Fact]
    public void SymbolicLinkIsNotOwned()
    {
        string target = this.CreateFolderInTempFolder(Guid.NewGuid().ToString());
        string link = Path.Combine(path1: this.CreateFolderInTempFolder(Guid.NewGuid().ToString()), path2: "link");
        Directory.CreateSymbolicLink(path: link, pathToTarget: target);

        Assert.Equal(expected: !OperatingSystem.IsLinux(), actual: this._fileOwnership.IsOwnedByEffectiveUser(link));
    }
}
