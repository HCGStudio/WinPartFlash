namespace WinPartFlash.Gui.Compression;

public interface ICompressionStreamCopierFactory
{
    ICompressionStreamCopier GetCopier(CompressionType compressionType);
}