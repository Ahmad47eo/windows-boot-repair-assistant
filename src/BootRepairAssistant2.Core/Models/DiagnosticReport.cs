namespace BootRepairAssistant2.Core;

public sealed class DiagnosticReport
{
    public const int MinimumEfiScoreMargin = 15;

    public FirmwareDetectionResult Firmware { get; init; } =
        new(FirmwareMode.Unknown, "None", "Not scanned");

    public bool IsWinPe { get; init; }

    public string WinPeDetails { get; init; } = string.Empty;

    public IReadOnlyList<VolumeInfo> VolumesExamined { get; init; } =
        Array.Empty<VolumeInfo>();

    public IReadOnlyList<WindowsInstallation> WindowsCandidates { get; init; } =
        Array.Empty<WindowsInstallation>();

    public WindowsInstallation? SelectedWindows { get; init; }

    public IReadOnlyList<EfiPartitionCandidate> EfiCandidates { get; init; } =
        Array.Empty<EfiPartitionCandidate>();

    public EfiPartitionCandidate? SelectedEfi { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

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

            var top = QualifiedEfiCandidates();
            return top.Count > 0
                && SelectedEfi is not null
                && Equals(SelectedEfi, top[0])
                && HasMinimumEfiScoreMargin(top);
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

            var top = QualifiedEfiCandidates();

            if (top.Count == 0)
            {
                return "No sufficiently trustworthy EFI System Partition candidate was found.";
            }

            if (!HasMinimumEfiScoreMargin(top))
            {
                return
                    $"EFI System Partition selection is ambiguous: top candidates " +
                    $"score {top[0].Score} and {top[1].Score} " +
                    $"(margin below {MinimumEfiScoreMargin}).";
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

    public StatusLevel EfiStatus
    {
        get
        {
            if (EfiCandidates.Count == 0)
            {
                return StatusLevel.Error;
            }

            var top = QualifiedEfiCandidates();
            return SelectedEfi is not null
                && top.Count > 0
                && Equals(SelectedEfi, top[0])
                && HasMinimumEfiScoreMargin(top)
                ? StatusLevel.Ok
                : StatusLevel.Warning;
        }
    }

    public StatusLevel EnvironmentStatus =>
        IsWinPe ? StatusLevel.Ok : StatusLevel.Warning;

    private List<EfiPartitionCandidate> QualifiedEfiCandidates()
    {
        return EfiCandidates
            .Where(candidate => candidate.Score >= 30)
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
    }

    private static bool HasMinimumEfiScoreMargin(
        IReadOnlyList<EfiPartitionCandidate> candidates)
    {
        return candidates.Count > 0
            && (candidates.Count == 1
                || candidates[0].Score - candidates[1].Score
                    >= MinimumEfiScoreMargin);
    }
}
