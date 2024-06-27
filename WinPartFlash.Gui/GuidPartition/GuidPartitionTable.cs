using System.Runtime.InteropServices;

namespace WinPartFlash.Gui.GuidPartition;

[StructLayout(LayoutKind.Sequential)]
public struct GuidPartitionTable
{
    public GuidPartitionTableHeader Header;
}