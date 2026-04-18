using System;
using System.IO;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Utils;

/// <summary>
/// Reads and writes GNU-sha256sum-format sidecar files (<c>&lt;hex&gt;  &lt;basename&gt;\n</c>)
/// placed next to an image as <c>&lt;image&gt;.sha256</c>.
/// </summary>
public static class ChecksumSidecar
{
    public const string Extension = ".sha256";

    public static string SidecarPathFor(string targetPath) => targetPath + Extension;

    public static string FormatLine(byte[] hash, string basename)
    {
        return Convert.ToHexString(hash).ToLowerInvariant() + "  " + basename + "\n";
    }

    public static Task WriteAsync(string targetPath, byte[] hash)
    {
        return File.WriteAllTextAsync(
            SidecarPathFor(targetPath),
            FormatLine(hash, Path.GetFileName(targetPath)));
    }

    public static byte[]? TryReadHash(string targetPath)
    {
        var path = SidecarPathFor(targetPath);
        if (!File.Exists(path)) return null;

        string firstLine;
        try
        {
            using var reader = new StreamReader(path);
            firstLine = reader.ReadLine() ?? string.Empty;
        }
        catch (IOException)
        {
            return null;
        }

        var trimmed = firstLine.TrimStart();
        var space = trimmed.IndexOf(' ');
        var hex = space < 0 ? trimmed.TrimEnd() : trimmed[..space];

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
