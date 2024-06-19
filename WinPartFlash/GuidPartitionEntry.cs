using System.Runtime.InteropServices;

namespace WinPartFlash;

[StructLayout(LayoutKind.Sequential, Size = 128)]
public unsafe struct GuidPartitionEntry
{
    public Guid PartitionTypeGuid;
    public Guid PartitionGuid;
    public ulong StartLba;
    public ulong EndLba;
    public GuidPartitionAttributeFlags Attribute;
    public fixed char PartitionName[36];
}