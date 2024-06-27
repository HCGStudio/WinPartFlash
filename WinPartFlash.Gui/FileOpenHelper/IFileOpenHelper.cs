using System.IO;
using System.Threading.Tasks;
using WinPartFlash.Gui.Compression;

namespace WinPartFlash.Gui.FileOpenHelper;

public interface IFileOpenHelper
{
    bool IsSupported(string name);
    Task<(Stream, CompressionType)> OpenRead(string name);
}