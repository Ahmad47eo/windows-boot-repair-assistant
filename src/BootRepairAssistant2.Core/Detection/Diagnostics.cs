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
        var initialReport = new DiagnosticReport
        {
            Firmware = firmwareResult,
            IsWinPe = winPeResult.IsWinPe,
            WinPeDetails = winPeResult.Details,
            VolumesExamined = volumesExamined,
            WindowsCandidates = windowsCandidates,
            SelectedWindows = selectedWindows,
            EfiCandidates = efiCandidates,
            SelectedEfi = selectedEfi,
            Notes = windows.LastNotes
        };
        var problems = string.IsNullOrWhiteSpace(initialReport.RepairBlockReason)
            ? Array.Empty<string>()
            : new[] { initialReport.RepairBlockReason };
        var report = new DiagnosticReport
        {
            Firmware = initialReport.Firmware,
            IsWinPe = initialReport.IsWinPe,
            WinPeDetails = initialReport.WinPeDetails,
            VolumesExamined = initialReport.VolumesExamined,
            WindowsCandidates = initialReport.WindowsCandidates,
            SelectedWindows = initialReport.SelectedWindows,
            EfiCandidates = initialReport.EfiCandidates,
            SelectedEfi = initialReport.SelectedEfi,
            Problems = problems,
            Notes = initialReport.Notes
        };
        log.Log(
            $"Diagnostics completed: RepairAllowed={report.RepairAllowed}; " +
            $"{report.Problems.Count} problem(s); {report.Notes.Count} note(s).");
        return Task.FromResult(report);
    }
}
