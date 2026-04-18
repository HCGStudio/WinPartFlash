namespace WinPartFlash.Gui.Compression;

/// <summary>
/// Per-format compression-level ranges. Each format exposes an integer scale
/// that the slider maps onto; copiers translate that integer into the
/// library-specific setting.
/// </summary>
public static class CompressionLevelInfo
{
    public static bool SupportsLevel(CompressionType type) => type is
        CompressionType.GzipCompress or
        CompressionType.Lz4Compress or
        CompressionType.ZstandardCompress;

    public static bool SupportsWorkers(CompressionType type) => type is CompressionType.ZstandardCompress;

    public static (int Min, int Max, int Default) LevelRange(CompressionType type) => type switch
    {
        CompressionType.GzipCompress => (1, 3, 2),
        CompressionType.Lz4Compress => (1, 12, 1),
        CompressionType.ZstandardCompress => (1, 19, 3),
        _ => (0, 0, 0)
    };
}
