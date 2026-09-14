namespace BootRepairAssistant2.Core;

public sealed class RepairEngine
{
    private readonly IProcessRunner runner;
    private readonly BackupManager backup;
    private readonly Verifier verifier;
    private readonly Diagnostics diagnostics;
    private readonly FirmwareDetector firmware;
    private readonly IVolumeMounter mounter;
    private readonly IEnvironmentInfo environment;
    private readonly IFileSystem fileSystem;
    private readonly ILogSink log;

    public RepairEngine(
        IProcessRunner runner,
        BackupManager backup,
        Verifier verifier,
        Diagnostics diagnostics,
        FirmwareDetector firmware,
        IVolumeMounter mounter,
        IEnvironmentInfo environment,
        IFileSystem fileSystem,
        ILogSink? log = null)
    {
        this.runner = runner;
        this.backup = backup;
        this.verifier = verifier;
        this.diagnostics = diagnostics;
        this.firmware = firmware;
        this.mounter = mounter;
        this.environment = environment;
        this.fileSystem = fileSystem;
        this.log = log ?? NullLogSink.Instance;
    }

    public string DescribePlannedCommand(DiagnosticReport report)
    {
        if (report.SelectedWindows is null
            || report.SelectedEfi is null)
        {
            return "No repair command can be planned from the diagnostic report.";
        }

        var windowsPath = report.SelectedWindows.WindowsPath;
        var windowsLetter = report.SelectedWindows.DriveLetter?
            .TrimEnd(':');
        var efiLetter = report.SelectedEfi.Volume.DriveLetter?
            .TrimEnd(':');
        if (windowsLetter is null || efiLetter is null)
        {
            var windowsPlaceholder = windowsLetter is null
                ? "<letter>:\\Windows"
                : windowsPath;
            var efiPlaceholder = efiLetter is null
                ? "<letter>:"
                : efiLetter + ":";
            var missing = windowsLetter is null && efiLetter is null
                ? $"Windows volume {report.SelectedWindows.VolumeGuidPath} and " +
                  $"EFI volume {report.SelectedEfi.Volume.VolumeGuidPath} have no drive letters"
                : windowsLetter is null
                    ? $"Windows volume {report.SelectedWindows.VolumeGuidPath} has no drive letter"
                    : $"EFI volume {report.SelectedEfi.Volume.VolumeGuidPath} has no drive letter";
            return
                $"Test Mode: {missing}; Repair would temporarily assign a free letter " +
                $"and run: bcdboot {windowsPlaceholder} /s {efiPlaceholder} /f UEFI";
        }

        var command = BuildCommand(windowsPath, efiLetter);
        return new CommandResult(
            command.FileName,
            command.Arguments,
            null,
            string.Empty,
            string.Empty,
            false,
            false,
            TimeSpan.Zero).DisplayCommand;
    }

    public async Task<RepairResult> RepairAsync(
        DiagnosticReport report,
        bool testMode,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (!report.RepairAllowed)
        {
            return new RepairResult(
                RepairOutcome.Failed,
                string.Empty,
                null,
                null,
                null,
                report.RepairBlockReason);
        }

        if (ct.IsCancellationRequested)
        {
            return new RepairResult(
                RepairOutcome.NotRun,
                string.Empty,
                null,
                null,
                null,
                "Repair cancelled before starting.");
        }

        if (firmware.Detect().Mode != FirmwareMode.Uefi)
        {
            return new RepairResult(
                RepairOutcome.Failed,
                string.Empty,
                null,
                null,
                null,
                "Firmware changed or UEFI is no longer detected.");
        }

        var backupDirectory = backup.CreateBackup(report);
        progress?.Report($"Backup created: {backupDirectory}");
        var efiLetter = report.SelectedEfi!.Volume.DriveLetter?
            .TrimEnd(':');
        var windowsLetter = report.SelectedWindows!.DriveLetter?
            .TrimEnd(':');

        if ((efiLetter is null || windowsLetter is null) && testMode)
        {
            return new RepairResult(
                RepairOutcome.NotRun,
                backupDirectory,
                null,
                null,
                null,
                DescribePlannedCommand(report) +
                " Backup was created in Test Mode; no mount or boot changes were made.");
        }

        if (ct.IsCancellationRequested)
        {
            return new RepairResult(
                RepairOutcome.NotRun,
                backupDirectory,
                null,
                null,
                null,
                "Repair cancelled before bcdboot.");
        }

        var assignedWindowsLetter = false;
        var assignedEfiLetter = false;
        try
        {
            if (windowsLetter is null)
            {
                windowsLetter = mounter.AssignLetter(
                    report.SelectedWindows.VolumeGuidPath);
                if (windowsLetter is null)
                {
                    return new RepairResult(
                        RepairOutcome.Failed,
                        backupDirectory,
                        null,
                        null,
                        null,
                        "Windows volume has no drive letter and no free temporary drive letter could be assigned.");
                }

                windowsLetter = windowsLetter.TrimEnd(':');
                assignedWindowsLetter = true;
                log.Log(
                    $"Temporarily assigned Windows volume {report.SelectedWindows.VolumeGuidPath} to {windowsLetter}:");
            }

            if (efiLetter is null)
            {
                efiLetter = mounter.AssignLetter(
                    report.SelectedEfi.Volume.VolumeGuidPath);
                if (efiLetter is null)
                {
                    return new RepairResult(
                        RepairOutcome.Failed,
                        backupDirectory,
                        null,
                        null,
                        null,
                        "EFI has no drive letter and no free temporary drive letter could be assigned.");
                }

                efiLetter = efiLetter.TrimEnd(':');
                assignedEfiLetter = true;
                log.Log(
                    $"Temporarily assigned EFI volume {report.SelectedEfi.Volume.VolumeGuidPath} to {efiLetter}:");
            }

            var windowsPath = windowsLetter + @":\Windows";
            var command = BuildCommand(
                windowsPath,
                efiLetter);
            var systemBcdBoot = Path.Combine(
                environment.SystemRoot,
                "System32",
                "bcdboot.exe");
            var fileName = fileSystem.FileExists(systemBcdBoot)
                ? systemBcdBoot
                : command.FileName;

            if (testMode)
            {
                var planned = new CommandResult(
                    fileName,
                    command.Arguments,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    TimeSpan.Zero);
                return new RepairResult(
                    RepairOutcome.NotRun,
                    backupDirectory,
                    planned,
                    null,
                    null,
                    $"Test Mode: would run {planned.DisplayCommand}. Backup was created; no boot changes were made.");
            }

            var commandResult = await runner.RunAsync(
                fileName,
                command.Arguments,
                TimeSpan.FromSeconds(120),
                CancellationToken.None);
            progress?.Report("bcdboot completed.");
            if (ct.IsCancellationRequested)
            {
                return new RepairResult(
                    RepairOutcome.NotRun,
                    backupDirectory,
                    commandResult,
                    null,
                    null,
                    "Repair cancelled after bcdboot completed; verification was not started.");
            }

            var verification = verifier.Verify(efiLetter + @":\");
            var postScan = await diagnostics.RunAsync(CancellationToken.None);
            var outcome = commandResult.ExitCode == 0
                ? verification.Outcome
                : RepairOutcome.Failed;
            var summary = commandResult.ExitCode == 0
                ? $"bcdboot completed with exit code 0; verification {verification.Outcome}."
                : $"bcdboot failed with exit code {commandResult.ExitCode}: {commandResult.StdErr}";
            return new RepairResult(
                outcome,
                backupDirectory,
                commandResult,
                verification,
                postScan,
                summary);
        }
        finally
        {
            if (assignedEfiLetter)
            {
                if (efiLetter is not null)
                {
                    mounter.RemoveLetter(efiLetter);
                    log.Log($"Removed temporary EFI drive letter {efiLetter}:");
                }
            }

            if (assignedWindowsLetter)
            {
                if (windowsLetter is not null)
                {
                    mounter.RemoveLetter(windowsLetter);
                    log.Log(
                        $"Removed temporary Windows drive letter {windowsLetter}:");
                }
            }
        }
    }

    private static (string FileName, IReadOnlyList<string> Arguments) BuildCommand(
        string windowsPath,
        string efiLetter)
    {
        return BcdBootCommandBuilder.Build(windowsPath, efiLetter);
    }
}
