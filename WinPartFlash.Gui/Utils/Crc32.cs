using System;

namespace WinPartFlash.Gui.Utils;

public static class Crc32
{
    private const uint Polynomial = 0xedb88320;
    private static readonly uint[] Table;

    static Crc32()
    {
        Table = InitializeTable(Polynomial);
    }

    public static uint Compute(ReadOnlySpan<byte> buffer, uint initValue = 0u)
    {
        if (System.Runtime.Intrinsics.Arm.Crc32.IsSupported) return ComputeArm(buffer, initValue);

        var crc = ~initValue;

        foreach (var b in buffer)
        {
            var tableIndex = (byte)((crc & 0xff) ^ b);
            crc = (crc >> 8) ^ Table[tableIndex];
        }

        return ~crc;
    }

    private static uint ComputeArm(ReadOnlySpan<byte> buffer, uint initValue = 0u)
    {
        var crc = ~initValue;
        foreach (var b in buffer) crc = System.Runtime.Intrinsics.Arm.Crc32.ComputeCrc32(crc, b);

        return ~crc;
    }

    private static uint[] InitializeTable(uint polynomial)
    {
        if (System.Runtime.Intrinsics.Arm.Crc32.IsSupported)
            // No need if system supports
            return [];

        var createTable = new uint[256];

        for (var i = 0u; i < 256; i++)
        {
            var entry = i;
            for (uint j = 0; j < 8; j++)
                if ((entry & 1) == 1)
                    entry = (entry >> 1) ^ polynomial;
                else
                    entry >>= 1;

            createTable[i] = entry;
        }

        return createTable;
    }
}