using System;
using System.Runtime.Versioning;
using Credfeto.Keys.Server.Cli.Native;

namespace Credfeto.Keys.Server.Cli;

public sealed class UnixFileOwnership : IFileOwnership
{
    private const ushort FILE_TYPE_MASK = 0xF000;
    private const ushort SYMBOLIC_LINK_TYPE = 0xA000;

    public bool IsOwnedByEffectiveUser(string path)
    {
        return !OperatingSystem.IsLinux() || IsOwnedByEffectiveUserOnLinux(path);
    }

    // A symbolic link is refused rather than followed, so the check cannot be pointed at a directory other than the
    // one named; a failed statx call (including one blocked by a seccomp profile) also counts as not owned.
    [SupportedOSPlatform("linux")]
    private static bool IsOwnedByEffectiveUserOnLinux(string path)
    {
        int result = NativeMethods.Statx(
            dirfd: NativeMethods.AT_FDCWD,
            path: path,
            flags: NativeMethods.AT_SYMLINK_NOFOLLOW,
            mask: NativeMethods.STATX_TYPE | NativeMethods.STATX_MODE | NativeMethods.STATX_UID,
            buffer: out StatxBuffer buffer
        );

        return result == 0
            && (buffer.Mask & NativeMethods.STATX_UID) != 0
            && (buffer.Mode & FILE_TYPE_MASK) != SYMBOLIC_LINK_TYPE
            && buffer.Uid == NativeMethods.GetEffectiveUserId();
    }
}
