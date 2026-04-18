namespace WinPartFlash.Gui.Compression;

public readonly record struct CompressionOptions(int? Level = null, int? Workers = null)
{
    public static CompressionOptions Default => default;
}
