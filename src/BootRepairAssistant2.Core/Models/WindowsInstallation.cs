namespace BootRepairAssistant2.Core;

public record WindowsInstallation(
    string? DriveLetter,
    string RootPath,
    string VolumeGuidPath,
    IReadOnlyList<string> MarkersFound,
    IReadOnlyList<string> MarkersMissing)
{
    public WindowsInstallation(
        string driveLetter,
        string windowsPath,
        IReadOnlyList<string> markersFound,
        IReadOnlyList<string> markersMissing)
        : this(
            driveLetter,
            windowsPath.EndsWith("Windows", StringComparison.OrdinalIgnoreCase)
                ? windowsPath[..^"Windows".Length]
                : windowsPath,
            string.Empty,
            markersFound,
            markersMissing)
    {
    }

    public string WindowsPath => RootPath + "Windows";

    public bool IsValid => MarkersMissing.Count == 0;
}
