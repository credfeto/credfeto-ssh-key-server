using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Credfeto.Keys.Server.Cli.Native;

[SupportedOSPlatform("linux")]
internal static partial class NativeMethods
{
    public const int AT_FDCWD = -100;
    public const int AT_SYMLINK_NOFOLLOW = 0x100;
    public const uint STATX_TYPE = 0x1;
    public const uint STATX_MODE = 0x2;
    public const uint STATX_UID = 0x8;

    [LibraryImport(
        libraryName: "libc",
        EntryPoint = "statx",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial int Statx(int dirfd, string path, int flags, uint mask, out StatxBuffer buffer);

    [LibraryImport(libraryName: "libc", EntryPoint = "geteuid")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial uint GetEffectiveUserId();
}
