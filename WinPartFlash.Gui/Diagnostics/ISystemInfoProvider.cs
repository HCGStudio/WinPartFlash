namespace WinPartFlash.Gui.Diagnostics;

public sealed record SystemInfoSnapshot(
    string AppVersion,
    string Runtime,
    string OperatingSystem,
    string NativeLibraryStatus,
    string PrivilegedHelperStatus);

public interface ISystemInfoProvider
{
    SystemInfoSnapshot GetSnapshot();
}
