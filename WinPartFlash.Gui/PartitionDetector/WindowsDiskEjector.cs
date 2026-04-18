using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("Windows")]
public sealed class WindowsDiskEjector : IDiskEjector
{
    private const uint IoctlStorageEjectMedia = 0x002D4808;
    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;
    private const uint IoctlStorageMediaRemoval = 0x002D4804;

    public Task EjectAsync(string diskDeviceId, CancellationToken cancellationToken = default)
    {
        var devicePath = diskDeviceId.StartsWith(@"\\.\")
            ? diskDeviceId
            : @"\\.\PhysicalDrive" + diskDeviceId;

        return Task.Run(() => EjectCore(devicePath), cancellationToken);
    }

    private static void EjectCore(string devicePath)
    {
        using var handle = CreateFileW(
            devicePath,
            0x80000000u | 0x40000000u, // GENERIC_READ | GENERIC_WRITE
            0x3u,                       // FILE_SHARE_READ | FILE_SHARE_WRITE
            IntPtr.Zero,
            3u,                         // OPEN_EXISTING
            0u,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException($"CreateFile({devicePath}) failed with {Marshal.GetLastWin32Error()}.");

        if (!DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new IOException($"LOCK_VOLUME failed with {Marshal.GetLastWin32Error()}.");

        DeviceIoControl(handle, FsctlDismountVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

        var allow = new byte[] { 1 };
        var allowHandle = GCHandle.Alloc(allow, GCHandleType.Pinned);
        try
        {
            DeviceIoControl(handle, IoctlStorageMediaRemoval,
                allowHandle.AddrOfPinnedObject(), (uint)allow.Length,
                IntPtr.Zero, 0, out _, IntPtr.Zero);
        }
        finally { allowHandle.Free(); }

        if (!DeviceIoControl(handle, IoctlStorageEjectMedia, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new IOException($"EJECT_MEDIA failed with {Marshal.GetLastWin32Error()}.");
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, uint nInBufferSize,
        IntPtr lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);
}
