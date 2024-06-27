using System.IO;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Compression;

/// <summary>
///     Interface for copying streams with compression.
/// </summary>
public interface ICompressionStreamCopier
{
    /// <summary>
    ///     Asynchronously copies data from the source stream to the output stream with compression.
    /// </summary>
    /// <param name="sourceStream">The source stream to read from.</param>
    /// <param name="outputStream">The output stream to write to.</param>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream);
}