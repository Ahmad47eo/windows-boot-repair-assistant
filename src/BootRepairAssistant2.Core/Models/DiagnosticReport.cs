namespace BootRepairAssistant2.Core;

public sealed class DiagnosticReport
{
    public FirmwareDetectionResult Firmware { get; init; } =
        new(FirmwareMode.Unknown, "None", "Not scanned");

    public bool IsWinPe { get; init; }

    public string WinPeDetails { get; init; } = string.Empty;

    public IReadOnlyList<WindowsInstallation> WindowsCandidates { get; init; } =
        Array.Empty<WindowsInstallation>();

    public WindowsInstallation? SelectedWindows { get; init; }

    public IReadOnlyList<EfiPartitionCandidate> EfiCandidates { get; init; } =
        Array.Empty<EfiPartitionCandidate>();

    public EfiPartitionCandidate? SelectedEfi { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();

    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    public bool RepairAllowed
    {
        get
        {
            if (Firmware.Mode != FirmwareMode.Uefi)
            {
                return false;
            }

            if (WindowsCandidates.Count(candidate => candidate.IsValid) != 1)
            {
                return false;
            }

            var top = EfiCandidates
                .Where(candidate => candidate.Score >= 30)
                .OrderByDescending(candidate => candidate.Score)
                .ToList();

            return top.Count == 1
                && SelectedEfi is not null
                && Equals(SelectedEfi, top[0]);
        }
    }

    public string RepairBlockReason
    {
        get
        {
            if (Firmware.Mode != FirmwareMode.Uefi)
            {
                return "Repair requires UEFI firmware; legacy BIOS or unknown firmware was detected.";
            }

            var windows = WindowsCandidates.Count(candidate => candidate.IsValid);
            if (windows != 1)
            {
                return windows == 0
                    ? "No unambiguous valid Windows installation was found."
                    : "Multiple valid Windows installations were found.";
            }

            var top = EfiCandidates
                .Where(candidate => candidate.Score >= 30)
                .OrderByDescending(candidate => candidate.Score)
                .ToList();

            if (top.Count == 0)
            {
                return "No sufficiently trustworthy EFI System Partition candidate was found.";
            }

            if (top.Count > 1 && top[0].Score == top[1].Score)
            {
                return "EFI System Partition selection is ambiguous because top candidates tie.";
            }

            if (SelectedEfi is null)
            {
                return "No EFI System Partition was selected.";
            }

            if (!Equals(SelectedEfi, top[0]))
            {
                return "Selected EFI System Partition is not the highest-confidence candidate.";
            }

            return string.Empty;
        }
    }

    public StatusLevel FirmwareStatus =>
        Firmware.Mode switch
        {
            FirmwareMode.Uefi => StatusLevel.Ok,
            FirmwareMode.LegacyBios => StatusLevel.Error,
            _ => StatusLevel.Unknown
        };

    public StatusLevel WindowsStatus =>
        WindowsCandidates.Count(candidate => candidate.IsValid) == 1
            ? StatusLevel.Ok
            : WindowsCandidates.Any(candidate => candidate.IsValid)
                ? StatusLevel.Warning
                : StatusLevel.Error;

    public StatusLevel EfiStatus =>
        RepairAllowed
            ? StatusLevel.Ok
            : EfiCandidates.Count > 0
                ? StatusLevel.Warning
                : StatusLevel.Error;

    public StatusLevel EnvironmentStatus =>
        IsWinPe ? StatusLevel.Ok : StatusLevel.Warning;
}
