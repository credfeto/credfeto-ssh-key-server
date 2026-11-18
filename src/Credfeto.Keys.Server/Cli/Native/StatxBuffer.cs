using System.Runtime.InteropServices;

namespace Credfeto.Keys.Server.Cli.Native;

// Layout of struct statx from linux/stat.h, which is the same on every architecture; only the fields read are declared.
[StructLayout(LayoutKind.Explicit, Size = 256)]
internal readonly struct StatxBuffer
{
    [FieldOffset(0)]
    public readonly uint Mask;

    [FieldOffset(20)]
    public readonly uint Uid;

    [FieldOffset(28)]
    public readonly ushort Mode;
}
