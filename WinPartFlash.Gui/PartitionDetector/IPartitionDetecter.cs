using System.Collections.Generic;

namespace WinPartFlash.Gui.PartitionDetector;

public interface IPartitionDetector
{
    IList<PartitionResult> DetectPartitions();
}