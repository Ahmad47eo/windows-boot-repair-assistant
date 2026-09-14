using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace BootRepairAssistant2.Core;

public sealed class WindowsVolumeProvider : IVolumeProvider
{
    private const uint IoctlDiskGetPartitionInfoEx = 0x00070048;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public IReadOnlyList<VolumeInfo> GetVolumes()
    {
        var result = new List<VolumeInfo>();
        if (!OperatingSystem.IsWindows())
        {
            return result;
        }

        var nameBuffer = new StringBuilder(1024);
        var handle = FindFirstVolume(nameBuffer, nameBuffer.Capacity);
        if (handle == InvalidHandleValue)
        {
            return result;
        }

        try
        {
            do
            {
                AddVolume(result, nameBuffer.ToString());
                nameBuffer.Clear();
            }
            while (FindNextVolume(handle, nameBuffer, nameBuffer.Capacity));
        }
        catch
        {
        }
        finally
        {
            FindVolumeClose(handle);
        }

        return result;
    }

    private static void AddVolume(List<VolumeInfo> result, string guidPath)
    {
        try
        {
            var pathBuffer = new StringBuilder(1024);
            GetVolumePathNamesForVolumeName(
                guidPath,
                pathBuffer,
                pathBuffer.Capacity,
                out _);

            var driveLetter = pathBuffer
                .ToString()
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?
                .TrimEnd('\\')
                .TrimEnd(':')
                .ToUpperInvariant();

            var fileSystemBuffer = new StringBuilder(64);
            var labelBuffer = new StringBuilder(256);
            GetVolumeInformation(
                guidPath,
                labelBuffer,
                labelBuffer.Capacity,
                out _,
                out _,
                out _,
                fileSystemBuffer,
                fileSystemBuffer.Capacity);

            var partition = GetPartitionInfo(guidPath);
            var size = partition.SizeBytes;
            if (size == 0 && driveLetter is not null)
            {
                var drive = new DriveInfo(driveLetter + ":");
                if (drive.IsReady)
                {
                    size = drive.TotalSize;
                }
            }

            result.Add(new VolumeInfo(
                driveLetter,
                guidPath,
                string.IsNullOrWhiteSpace(fileSystemBuffer.ToString())
                    ? null
                    : fileSystemBuffer.ToString(),
                size,
                partition.PartitionType,
                partition.IsGpt,
                labelBuffer.ToString()));
        }
        catch
        {
        }
    }

    private static (bool IsGpt, Guid? PartitionType, long SizeBytes) GetPartitionInfo(
        string path)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(
                path.TrimEnd('\\'),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                FileOptions.None);

            var buffer = new byte[144];
            var succeeded = DeviceIoControl(
                handle,
                IoctlDiskGetPartitionInfoEx,
                IntPtr.Zero,
                0,
                buffer,
                buffer.Length,
                out _,
                IntPtr.Zero);

            if (!succeeded)
            {
                return (false, null, 0);
            }

            var style = BitConverter.ToInt32(buffer, 0);
            var size = BitConverter.ToInt64(buffer, 16);
            if (style != 1)
            {
                return (false, null, size);
            }

            return (true, new Guid(buffer.AsSpan(32, 16)), size);
        }
        catch
        {
            return (false, null, 0);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstVolume(
        StringBuilder volumeName,
        int volumeNameSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool FindNextVolume(
        IntPtr handle,
        StringBuilder volumeName,
        int volumeNameSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindVolumeClose(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumePathNamesForVolumeName(
        string volumeName,
        StringBuilder volumePathNames,
        int bufferLength,
        out int returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder volumeName,
        int volumeNameSize,
        out uint serialNumber,
        out uint maxComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemName,
        int fileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint code,
        IntPtr inBuffer,
        int inSize,
        [Out] byte[] outBuffer,
        int outSize,
        out int returned,
        IntPtr overlapped);
}
