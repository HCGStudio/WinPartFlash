using System;
using System.Text;

namespace WinPartFlash.Gui.Inspection;

public static class HexDump
{
    private const int BytesPerRow = 16;

    public static string Format(ReadOnlySpan<byte> data, long baseOffset)
    {
        var sb = new StringBuilder(data.Length * 4);
        for (var row = 0; row < data.Length; row += BytesPerRow)
        {
            sb.Append((baseOffset + row).ToString("X10"));
            sb.Append("  ");

            for (var i = 0; i < BytesPerRow; i++)
            {
                if (row + i < data.Length)
                    sb.Append(data[row + i].ToString("X2"));
                else
                    sb.Append("  ");
                sb.Append(i == BytesPerRow / 2 - 1 ? "  " : ' ');
            }

            sb.Append(' ');
            for (var i = 0; i < BytesPerRow && row + i < data.Length; i++)
            {
                var b = data[row + i];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }

            sb.Append('\n');
        }
        return sb.ToString();
    }
}
