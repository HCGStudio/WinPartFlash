using System;
using System.Runtime.InteropServices;

namespace WinPartFlash.Gui.GuidPartition;

[StructLayout(LayoutKind.Sequential, Size = 92)]
public struct GuidPartitionTableHeader
{
    public ulong Signature;
    public uint Revision;
    public uint HeaderSize;
    public uint CrcValue;
    public uint ReservedZero;
    public ulong CurrentLba;
    public ulong BackupLba;
    public ulong FirstUsableLba;
    public ulong LastUsableLba;
    public Guid DiskGuid;
    public ulong PartitionEntriesStartLba;
    public uint PartitionEntriesCount;
    public uint PartitionEntrySize;
    public uint PartitionEntriesCrc;
}