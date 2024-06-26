using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WinPartFlash.Gui.Compression;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.FileOpenHelper;

public class FileOpenHelper(IHttpClientFactory clientFactory) : IFileOpenHelper
{
    private static readonly Dictionary<string, CompressionType> ContentTypeMapping =
        new()
        {
            { FileNameHelpers.ContentTypeGzip, CompressionType.GzipDecompress },
            { FileNameHelpers.ContentTypeGzipDeprecated, CompressionType.GzipDecompress },
            { FileNameHelpers.ContentTypeXz, CompressionType.XzDecompress },
            { FileNameHelpers.ContentTypeLz4, CompressionType.Lz4Compress },
            { FileNameHelpers.ContentTypeZstandard, CompressionType.ZstandardDecompress }
        };

    private readonly IReadOnlyList<IFileOpenHelper> _helpers =
    [
        new LocalOpenHelper(),
        new HttpOpenHelper(clientFactory.CreateClient(nameof(FileOpenHelper)))
    ];

    private static CompressionType DetectFromFileName(string fileName)
    {
        fileName = fileName.Trim('\"');
        if (fileName.EndsWith(FileNameHelpers.FileExtensionGzip))
            return CompressionType.GzipDecompress;
        if (fileName.EndsWith(FileNameHelpers.FileExtensionLz4))
            return CompressionType.Lz4Decompress;
        if (fileName.EndsWith(FileNameHelpers.FileExtensionXz))
            return CompressionType.XzDecompress;
        if (fileName.EndsWith(FileNameHelpers.FileExtensionLz4))
            return CompressionType.Lz4Decompress;
        if (fileName.EndsWith(FileNameHelpers.FileExtensionZstandard))
            return CompressionType.ZstandardDecompress;

        return CompressionType.Raw;
    }

    private static CompressionType? DetectFromContentType(string? contentType)
    {
        if (contentType == null)
            return null;
        return ContentTypeMapping.TryGetValue(contentType, out var value) ? value : null;
    }

    public bool IsSupported(string name)
    {
        return _helpers.Any(helper => helper.IsSupported(name));
    }

    public async Task<(Stream, CompressionType)> OpenRead(string name)
    {
        var helper = _helpers.FirstOrDefault(helper => helper.IsSupported(name));
        if (helper == null)
            throw new NotSupportedException();

        return await helper.OpenRead(name);
    }

    private class HttpOpenHelper(HttpClient client) : IFileOpenHelper
    {
        public bool IsSupported(string name)
        {
            return name.StartsWith("https://");
        }

        public async Task<(Stream, CompressionType)> OpenRead(string name)
        {
            var request = await client.GetAsync(name, HttpCompletionOption.ResponseHeadersRead);
            if (!request.IsSuccessStatusCode)
                throw new HttpRequestException(await request.Content.ReadAsStringAsync());

            var typeFromContentType = DetectFromContentType(request.Content.Headers.ContentType?.MediaType);
            if (typeFromContentType != null)
                return (await request.Content.ReadAsStreamAsync(), typeFromContentType.Value);

            var remoteFileName = request.Content.Headers.ContentDisposition?.FileName;
            return (await request.Content.ReadAsStreamAsync(), DetectFromFileName(remoteFileName ?? name));
        }
    }

    private class LocalOpenHelper : IFileOpenHelper
    {
        public bool IsSupported(string name)
        {
            return File.Exists(name);
        }

        public Task<(Stream, CompressionType)> OpenRead(string name)
        {
            return Task.FromResult<(Stream, CompressionType)>((File.OpenRead(name), DetectFromFileName(name)));
        }
    }
}