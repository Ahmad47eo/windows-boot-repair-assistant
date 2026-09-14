using System.Runtime.InteropServices;

namespace BootRepairAssistant2.Core;

public sealed class WindowsVolumeMounter : IVolumeMounter
{
    public string? AssignLetter(string volumeGuidPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        for (var letter = 'Z'; letter >= 'D'; letter--)
        {
            var mountPoint = letter + @":\";
            if (Directory.Exists(mountPoint))
            {
                continue;
            }

            var volumeName = volumeGuidPath.EndsWith(
                "\\",
                StringComparison.Ordinal)
                ? volumeGuidPath
                : volumeGuidPath + "\\";
            if (SetVolumeMountPoint(mountPoint, volumeName))
            {
                return letter.ToString();
            }
        }

        return null;
    }

    public void RemoveLetter(string letter)
    {
        if (OperatingSystem.IsWindows())
        {
            DeleteVolumeMountPoint(letter.TrimEnd(':') + @":\");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetVolumeMountPoint(
        string mountPoint,
        string volumeName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteVolumeMountPoint(string mountPoint);
}
