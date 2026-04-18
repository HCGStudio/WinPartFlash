using System;
using System.IO;
using System.Threading;
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
    /// <param name="options">Format-specific knobs (level, worker count). Ignored by decompressors and the raw copier.</param>
    /// <param name="progress">Optional reporter; value is cumulative bytes moved along the raw-partition-facing side of the copy.</param>
    /// <param name="cancellationToken">Token to cancel the copy operation.</param>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);
}
