using BootRepairAssistant2.Core;

namespace BootRepairAssistant2;

internal sealed partial class MainForm : Form
{
    private readonly Diagnostics diagnostics;
    private readonly RepairEngine repair;
    private readonly Verifier verifier;
    private readonly BackupManager backup;
    private readonly BootRepairLogger logger;
    private DiagnosticReport? report;
    private RepairResult? repairResult;
    private CancellationTokenSource? cancellation;

    public MainForm(
        Diagnostics diagnostics,
        RepairEngine repair,
        Verifier verifier,
        BackupManager backup,
        BootRepairLogger logger)
    {
        this.diagnostics = diagnostics;
        this.repair = repair;
        this.verifier = verifier;
        this.backup = backup;
        this.logger = logger;

        InitializeLayout();
        WireEvents();
    }

    private void WireEvents()
    {
        scan.Click += async (_, _) => await RunScan(false);
        diagnose.Click += async (_, _) => await RunScan(true);
        repairButton.Click += async (_, _) => await RunRepair();
        verifyButton.Click += async (_, _) => await RunVerify();
        viewLog.Click += (_, _) => output.Text = logger.GetLogText();
        copy.Click += (_, _) => CopyReport();
        cancel.Click += (_, _) => cancellation?.Cancel();
        testMode.CheckedChanged += (_, _) =>
        {
            repairButton.Text = testMode.Checked
                ? "Repair Boot (Test Mode – simulate)"
                : "Repair Boot";
        };
    }

    private async Task RunScan(bool detailed)
    {
        await RunBusy(async ct =>
        {
            report = await Task.Run(
                () => diagnostics.RunAsync(ct),
                ct);
            UpdateStatus();
            output.Text = detailed
                ? BuildReport()
                : $"Scan complete. Repair allowed: {report.RepairAllowed}{Environment.NewLine}" +
                  report.RepairBlockReason;
            repairButton.Enabled = report.RepairAllowed;
        });
    }

    private async Task RunRepair()
    {
        if (report is null || !report.RepairAllowed)
        {
            return;
        }

        try
        {
            var command = repair.DescribePlannedCommand(report);
            var backupBase = backup.ResolveBackupBase(report);
            using var dialog = new ConfirmRepairDialog(
                report,
                command,
                backupBase,
                testMode.Checked);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await RunBusy(async ct =>
            {
                repairResult = await Task.Run(
                    () => repair.RepairAsync(
                        report,
                        testMode.Checked,
                        new Progress<string>(message =>
                            output.AppendText(
                                message + Environment.NewLine)),
                        ct),
                    ct);
                output.Text = BuildReport();
            });
        }
        catch (Exception exception)
        {
            HandleError(exception);
        }
    }

    private async Task RunVerify()
    {
        if (report?.SelectedEfi is null)
        {
            output.Text = "Verify requires a selected EFI partition.";
            return;
        }

        var efiVolume = report.SelectedEfi.Volume;
        var root = efiVolume.DriveLetter is string letter
            ? letter.TrimEnd(':') + @":\"
            : EnsureTrailingSeparator(efiVolume.VolumeGuidPath);
        await RunBusy(async ct =>
        {
            var result = await Task.Run(
                () => verifier.Verify(root),
                ct);
            report = await Task.Run(
                () => diagnostics.RunAsync(ct),
                ct);
            output.Text = string.Join(
                Environment.NewLine,
                result.Checks.Select(check =>
                    $"{check.Check}: {(check.Passed ? "PASS" : "FAIL")} - {check.Detail}"));
            output.AppendText(Environment.NewLine + BuildReport());
            UpdateStatus();
        });
    }

    private async Task RunBusy(Func<CancellationToken, Task> operation)
    {
        SetBusy(true);
        cancellation = new CancellationTokenSource();
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            output.Text = "Operation cancelled. No further steps were started.";
        }
        catch (Exception exception)
        {
            HandleError(exception);
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        scan.Enabled = !busy;
        diagnose.Enabled = !busy;
        repairButton.Enabled = !busy && report?.RepairAllowed == true;
        verifyButton.Enabled = !busy;
        viewLog.Enabled = !busy;
        copy.Enabled = !busy;
        testMode.Enabled = !busy;
        cancel.Enabled = busy;
        output.ReadOnly = true;
    }

    private void UpdateStatus()
    {
        if (report is null)
        {
            return;
        }

        firmwareStatus.ForeColor = ColorFor(report.FirmwareStatus);
        windowsStatus.ForeColor = ColorFor(report.WindowsStatus);
        efiStatus.ForeColor = ColorFor(report.EfiStatus);
        environmentStatus.ForeColor = ColorFor(report.EnvironmentStatus);
        SetDetail(
            firmwareStatus,
            $"{StatusText.ForFirmware(report.Firmware)} ({report.Firmware.Details})");
        SetDetail(
            windowsStatus,
            report.SelectedWindows is null
                ? $"No valid Windows installation found on " +
                  $"{report.VolumesExamined.Count} volume(s) — see output"
                : $"Windows installation found: {report.SelectedWindows.WindowsPath}");
        SetDetail(
            efiStatus,
            report.SelectedEfi is null
                ? "No unambiguous EFI System Partition"
                : $"EFI System Partition found: {report.SelectedEfi.Volume.FileSystem}, " +
                  $"{report.SelectedEfi.Volume.SizeBytes / (1024 * 1024)} MB, " +
                  $"{(report.SelectedEfi.Volume.DriveLetter is null ? "no letter assigned" : "letter " + report.SelectedEfi.Volume.DriveLetter)}");
        SetDetail(
            environmentStatus,
            (report.IsWinPe ? "Windows PE detected" : "Windows PE not detected") +
            $": {report.WinPeDetails}");
    }

    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(BuildReport());
        }
        catch (Exception exception)
        {
            output.Text = $"Copy failed: {exception.Message}";
            logger.Log($"Clipboard copy failed: {exception}");
        }
    }

    private void HandleError(Exception exception)
    {
        logger.Log(exception.ToString());
        output.Text =
            $"What failed: {exception.Message}{Environment.NewLine}" +
            $"Why it matters: the requested operation did not complete.{Environment.NewLine}" +
            "Safe to continue: diagnostics only.";
        MessageBox.Show(
            this,
            exception.Message,
            "Operation failed",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private string BuildReport()
    {
        return logger.BuildReport(report, repairResult);
    }

    private static Color ColorFor(StatusLevel level)
    {
        return level switch
        {
            StatusLevel.Ok => Color.Green,
            StatusLevel.Warning => Color.Goldenrod,
            StatusLevel.Error => Color.Red,
            _ => Color.Gray
        };
    }

    private static void SetDetail(Label indicator, string text)
    {
        if (indicator.Parent is FlowLayoutPanel panel
            && panel.Controls.Count > 1
            && panel.Controls[1] is Label label)
        {
            label.Text = text;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith('\\') || path.EndsWith('/')
            ? path
            : path + '\\';
    }
}
