using System.Runtime.InteropServices;

namespace WinPartFlash;

[StructLayout(LayoutKind.Sequential)]
public struct GuidPartitionTable
{
    public GuidPartitionTableHeader Header;
}