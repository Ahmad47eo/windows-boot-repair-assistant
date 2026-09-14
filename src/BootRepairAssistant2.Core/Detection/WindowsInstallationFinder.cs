namespace BootRepairAssistant2.Core;

public sealed class WindowsInstallationFinder
{
    private readonly IVolumeProvider volumes;
    private readonly IFileSystem fileSystem;
    private readonly ILogSink log;

    public IReadOnlyList<VolumeInfo> LastExaminedVolumes { get; private set; } =
        Array.Empty<VolumeInfo>();

    public IReadOnlyList<string> LastProblems { get; private set; } =
        Array.Empty<string>();

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
        var examined = volumes.GetVolumes().ToList();
        var problems = new List<string>();
        foreach (var volume in examined)
        {
            var drive = volume.DriveLetter?
                .TrimEnd(':')
                .ToUpperInvariant();
            var displayDrive = drive ?? "(no letter)";
            log.Log(
                $"Volume {displayDrive} {volume.VolumeGuidPath} " +
                $"fs={FormatFileSystem(volume.FileSystem)} " +
                $"size={volume.SizeBytes / (1024 * 1024)} MB " +
                $"label={FormatLabel(volume.Label)}");

            if (volume.SizeBytes > 0
                && (string.IsNullOrWhiteSpace(volume.FileSystem)
                    || string.Equals(
                        volume.FileSystem,
                        "RAW",
                        StringComparison.OrdinalIgnoreCase)))
            {
                var problem =
                    $"Volume {displayDrive} {volume.VolumeGuidPath} " +
                    "possibly BitLocker-locked or unformatted";
                problems.Add(problem);
                log.Log(problem);
            }

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

            var rootPath = drive is null
                ? EnsureTrailingSeparator(volume.VolumeGuidPath)
                : drive + @":\";
            var windowsRoot = rootPath + "Windows";
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
                var path = CombineRoot(windowsRoot, marker.Relative);
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

            log.Log(
                $"Volume {displayDrive} markers found: " +
                $"{FormatMarkers(found)}; missing: {FormatMarkers(missing)}");
            if (fileSystem.DirectoryExists(
                    CombineRoot(windowsRoot, @"System32\")))
            {
                result.Add(new WindowsInstallation(
                    drive,
                    rootPath,
                    volume.VolumeGuidPath,
                    found,
                    missing));
            }
        }

        LastExaminedVolumes = examined;
        LastProblems = problems;
        log.Log(
            $"Windows candidates: " +
            $"{result.Count(candidate => candidate.IsValid)} valid / " +
            $"{examined.Count} examined");
        return result;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith('\\') || path.EndsWith('/')
            ? path
            : path + '\\';
    }

    private static string CombineRoot(string root, string relativePath)
    {
        return root.TrimEnd('\\', '/')
            + "\\"
            + relativePath.TrimStart('\\', '/');
    }

    private static string FormatFileSystem(string? fileSystem)
    {
        return string.IsNullOrWhiteSpace(fileSystem)
            ? "unknown"
            : fileSystem;
    }

    private static string FormatLabel(string? label)
    {
        return string.IsNullOrWhiteSpace(label)
            ? "unknown"
            : label;
    }

    private static string FormatMarkers(IReadOnlyList<string> markers)
    {
        return markers.Count == 0
            ? "none"
            : string.Join(", ", markers);
    }
}
