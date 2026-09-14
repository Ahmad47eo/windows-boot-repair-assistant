namespace BootRepairAssistant2.Core;

public sealed class WindowsInstallationFinder
{
    private readonly IVolumeProvider volumes;
    private readonly IFileSystem fileSystem;
    private readonly ILogSink log;

    public WindowsInstallationFinder(
        IVolumeProvider volumes,
        IFileSystem fileSystem,
        ILogSink? log = null)
    {
        this.volumes = volumes;
        this.fileSystem = fileSystem;
        this.log = log ?? NullLogSink.Instance;
    }

    public IReadOnlyList<WindowsInstallation> Find(string? winPeDrive = null)
    {
        var result = new List<WindowsInstallation>();
        foreach (var volume in volumes.GetVolumes().Where(volume =>
                     !string.IsNullOrWhiteSpace(volume.DriveLetter)))
        {
            var drive = volume.DriveLetter!
                .TrimEnd(':')
                .ToUpperInvariant();
            var skippedDrive = (winPeDrive ?? "X")
                .TrimEnd(':')
                .ToUpperInvariant();

            if (string.Equals(
                    drive,
                    skippedDrive,
                    StringComparison.OrdinalIgnoreCase))
            {
                log.Log($"Skipping {drive}: because it is the WinPE ramdisk.");
                continue;
            }

            var windowsRoot = drive + @":\Windows";
            var markers = new[]
            {
                (Relative: @"System32\", IsDirectory: true, Required: true),
                (Relative: @"System32\ntoskrnl.exe", IsDirectory: false, Required: true),
                (Relative: @"System32\config\SYSTEM", IsDirectory: false, Required: true),
                (Relative: @"System32\winload.efi", IsDirectory: false, Required: true),
                (Relative: @"explorer.exe", IsDirectory: false, Required: false),
                (Relative: @"Boot\EFI\bootmgfw.efi", IsDirectory: false, Required: false)
            };

            var found = new List<string>();
            var missing = new List<string>();
            foreach (var marker in markers)
            {
                var path = Path.Combine(windowsRoot, marker.Relative);
                var present = marker.IsDirectory
                    ? fileSystem.DirectoryExists(path)
                    : fileSystem.FileExists(path);

                if (present)
                {
                    found.Add(marker.Relative);
                }
                else if (marker.Required)
                {
                    missing.Add(marker.Relative);
                }
            }

            result.Add(new WindowsInstallation(
                drive,
                windowsRoot,
                found,
                missing));
        }

        return result;
    }
}
