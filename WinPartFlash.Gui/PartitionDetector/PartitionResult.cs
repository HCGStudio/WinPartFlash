using System;
using System.IO;

namespace WinPartFlash.Gui.PartitionDetector;

public record PartitionResult(string Name, ulong Length, Lazy<Stream> OpenFileStream);