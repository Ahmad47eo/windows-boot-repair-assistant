namespace BootRepairAssistant2.Core;

public sealed class Diagnostics
{
    private readonly FirmwareDetector firmware;
    private readonly WinPeDetector winPe;
    private readonly WindowsInstallationFinder windows;
    private readonly EfiPartitionFinder efi;
    private readonly ILogSink log;

    public Diagnostics(
        FirmwareDetector firmware,
        WinPeDetector winPe,
        WindowsInstallationFinder windows,
        EfiPartitionFinder efi,
        ILogSink? log = null)
    {
        this.firmware = firmware;
        this.winPe = winPe;
        this.windows = windows;
        this.efi = efi;
        this.log = log ?? NullLogSink.Instance;
    }

    public Task<DiagnosticReport> RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var firmwareResult = firmware.Detect();
        var winPeResult = winPe.Detect();
        var windowsCandidates = windows.Find(
            winPeResult.IsWinPe ? "X" : null);
        var volumesExamined = windows.LastExaminedVolumes;
        var validWindows = windowsCandidates
            .Where(candidate => candidate.IsValid)
            .ToList();
        var selectedWindows = validWindows.Count == 1
            ? validWindows[0]
            : null;
        var efiCandidates = efi.Find(
            selectedWindows?.DriveLetter,
            selectedWindows?.VolumeGuidPath);
        var orderedEfi = efiCandidates
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
        var selectedEfi = orderedEfi.FirstOrDefault();
        var problems = windows.LastProblems.ToList();

        if (firmwareResult.Mode != FirmwareMode.Uefi)
        {
            problems.Add(
                $"Firmware is {firmwareResult.Mode}; UEFI is required for this repair. Safe to continue: diagnostics only.");
        }

        if (validWindows.Count == 0)
        {
            problems.Add(
                "No valid Windows installation was found; required system markers are missing. Repair is blocked.");
            problems.Add("Volumes examined:");
            problems.AddRange(volumesExamined.Select(FormatVolume));
        }
        else if (validWindows.Count > 1)
        {
            problems.Add(
                "Multiple valid Windows installations were found; selecting one would be unsafe.");
        }

        if (orderedEfi.Count == 0)
        {
            problems.Add(
                "No EFI candidate met the confidence threshold. Repair is blocked.");
        }
        else if (orderedEfi.Count > 1
                 && orderedEfi[0].Score == orderedEfi[1].Score)
        {
            selectedEfi = null;
            problems.Add(
                "Top EFI candidates have the same score, so the EFI partition is ambiguous.");
        }
        else if (orderedEfi[0].Volume.GptPartitionType != EfiPartitionFinder.EspType
                 && !orderedEfi[0].Reasons.Contains(
                     "Microsoft EFI boot directory"))
        {
            problems.Add(
                "The top EFI candidate is not GPT ESP-typed and lacks a Microsoft boot directory; repair is blocked.");
        }

        if (!winPeResult.IsWinPe)
        {
            problems.Add(
                "Windows PE was not detected. Read-only diagnostics are safe; repair is intended for WinPE.");
        }

        var report = new DiagnosticReport
        {
            Firmware = firmwareResult,
            IsWinPe = winPeResult.IsWinPe,
            WinPeDetails = winPeResult.Details,
            VolumesExamined = volumesExamined,
            WindowsCandidates = windowsCandidates,
            SelectedWindows = selectedWindows,
            EfiCandidates = efiCandidates,
            SelectedEfi = selectedEfi,
            Problems = problems
        };
        log.Log(
            $"Diagnostics completed: RepairAllowed={report.RepairAllowed}; {report.Problems.Count} problem(s).");
        return Task.FromResult(report);
    }

    private static string FormatVolume(VolumeInfo volume)
    {
        var drive = string.IsNullOrWhiteSpace(volume.DriveLetter)
            ? "(no letter)"
            : volume.DriveLetter;
        var fileSystem = string.IsNullOrWhiteSpace(volume.FileSystem)
            ? "unknown"
            : volume.FileSystem;
        var label = string.IsNullOrWhiteSpace(volume.Label)
            ? "unknown"
            : volume.Label;
        return
            $"  {drive} {volume.VolumeGuidPath} " +
            $"fs={fileSystem} size={volume.SizeBytes / (1024 * 1024)} MB " +
            $"label={label}";
    }
}
