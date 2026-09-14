namespace BootRepairAssistant2.Core;

public record WindowsInstallation(
    string DriveLetter,
    string WindowsPath,
    IReadOnlyList<string> MarkersFound,
    IReadOnlyList<string> MarkersMissing)
{
    public bool IsValid => MarkersMissing.Count == 0;
}
