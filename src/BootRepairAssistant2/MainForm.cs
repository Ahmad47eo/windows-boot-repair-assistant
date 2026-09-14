using BootRepairAssistant2.Core;
using System.Text;

namespace BootRepairAssistant2;

internal sealed class MainForm : Form
{
    private readonly Diagnostics diagnostics; private readonly RepairEngine repair; private readonly Verifier verifier; private readonly ILogSink logger;
    private readonly Label firmwareStatus = StatusLabel(); private readonly Label windowsStatus = StatusLabel(); private readonly Label efiStatus = StatusLabel(); private readonly Label environmentStatus = StatusLabel();
    private readonly TextBox output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 11) };
    private readonly CheckBox testMode = new() { Text = "Test Mode (no changes will be made)", Checked = true, AutoSize = true, Font = new Font("Segoe UI", 12) };
    private readonly Button scan = new() { Text = "Scan PC" }; private readonly Button diagnose = new() { Text = "Diagnose Only" }; private readonly Button repairButton = new() { Text = "Repair Boot" };
    private readonly Button verifyButton = new() { Text = "Verify Repair" }; private readonly Button viewLog = new() { Text = "View Log" }; private readonly Button copy = new() { Text = "Copy Report" }; private readonly Button cancel = new() { Text = "Cancel", Enabled = false };
    private DiagnosticReport? report; private RepairResult? repairResult; private CancellationTokenSource? cancellation;
    public MainForm(Diagnostics diagnostics, RepairEngine repair, Verifier verifier, ILogSink logger)
    {
        this.diagnostics = diagnostics; this.repair = repair; this.verifier = verifier; this.logger = logger;
        Text = "Windows Boot Repair Assistant 2"; MinimumSize = new Size(1000, 720); Width = 1200; Height = 820; Font = new Font("Segoe UI", 12);
        var title = new Label { Text = "Windows Boot Repair Assistant 2", Font = new Font("Segoe UI", 20, FontStyle.Bold), Dock = DockStyle.Top, Height = 50 };
        var warning = new Label { Text = "Do not use Repair unless the diagnostic checks are all successful.", ForeColor = Color.DarkRed, Font = new Font("Segoe UI", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 32 };
        var statuses = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 2, RowCount = 4 };
        statuses.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); statuses.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddStatus(statuses, 0, "Firmware", firmwareStatus); AddStatus(statuses, 1, "Windows", windowsStatus); AddStatus(statuses, 2, "EFI", efiStatus); AddStatus(statuses, 3, "Environment", environmentStatus);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 95, WrapContents = true };
        foreach (var button in new[] { scan, diagnose, repairButton, verifyButton, viewLog, copy, cancel }) { button.Font = new Font("Segoe UI", 14, FontStyle.Bold); button.AutoSize = true; actions.Controls.Add(button); }
        actions.Controls.Add(testMode);
        var outputPanel = new GroupBox { Text = "Detailed output and log", Dock = DockStyle.Fill, Padding = new Padding(8) }; outputPanel.Controls.Add(output);
        Controls.Add(outputPanel); Controls.Add(actions); Controls.Add(statuses); Controls.Add(warning); Controls.Add(title);
        scan.Click += async (_, _) => await RunScan(false); diagnose.Click += async (_, _) => await RunScan(true); repairButton.Click += async (_, _) => await RunRepair(); verifyButton.Click += async (_, _) => await RunVerify(); viewLog.Click += (_, _) => output.Text = logger is BootRepairLogger bl ? bl.BuildReport(report, repairResult) : output.Text; copy.Click += (_, _) => Clipboard.SetText(logger is BootRepairLogger b ? b.BuildReport(report, repairResult) : output.Text); cancel.Click += (_, _) => cancellation?.Cancel(); testMode.CheckedChanged += (_, _) => repairButton.Text = testMode.Checked ? "Repair Boot (Test Mode – simulate)" : "Repair Boot";
        repairButton.Enabled = false; repairButton.Text = "Repair Boot (Test Mode – simulate)";
    }
    private static Label StatusLabel() => new() { Text = "●", AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Gray };
    private static void AddStatus(TableLayoutPanel table, int row, string name, Label indicator)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); var label = new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 12, FontStyle.Bold) }; var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; panel.Controls.Add(indicator); panel.Controls.Add(new Label { Name = name + "Detail", AutoSize = true }); table.Controls.Add(label, 0, row); table.Controls.Add(panel, 1, row);
    }
    private async Task RunScan(bool detailed)
    {
        await RunBusy(async ct => { report = await diagnostics.RunAsync(ct); UpdateStatus(); output.Text = detailed ? BuildReport() : $"Scan complete. Repair allowed: {report.RepairAllowed}\r\n{report.RepairBlockReason}"; repairButton.Enabled = report.RepairAllowed; });
    }
    private async Task RunRepair()
    {
        if (report is null || !report.RepairAllowed) return;
        var command = BcdBootCommandBuilder.Build(report.SelectedWindows!.WindowsPath, report.SelectedEfi!.Volume.DriveLetter!.TrimEnd(':'));
        using var dialog = new ConfirmRepairDialog(report, string.Join(" ", new[] { command.FileName }.Concat(command.Arguments)), "Will be selected before operation", testMode.Checked);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunBusy(async ct => { repairResult = await repair.RepairAsync(report, testMode.Checked, new Progress<string>(s => output.AppendText(s + Environment.NewLine)), ct); output.Text = BuildReport(); });
    }
    private async Task RunVerify()
    {
        if (report?.SelectedEfi?.Volume.DriveLetter is not string letter) { output.Text = "Verify requires a lettered EFI partition."; return; }
        await RunBusy(async ct => { var result = verifier.Verify(letter.TrimEnd(':')); report = await diagnostics.RunAsync(ct); output.Text = string.Join(Environment.NewLine, result.Checks.Select(x => $"{x.Check}: {(x.Passed ? "PASS" : "FAIL")} - {x.Detail}")) + Environment.NewLine + BuildReport(); UpdateStatus(); });
    }
    private async Task RunBusy(Func<CancellationToken, Task> operation)
    {
        SetBusy(true); cancellation = new CancellationTokenSource();
        try { await operation(cancellation.Token); } catch (OperationCanceledException) { output.Text = "Operation cancelled. No further steps were started."; } catch (Exception ex) { logger.Log(ex.ToString()); output.Text = $"What failed: {ex.Message}\r\nWhy it matters: the requested operation did not complete.\r\nSafe to continue: diagnostics only."; MessageBox.Show(this, ex.Message, "Operation failed", MessageBoxButtons.OK, MessageBoxIcon.Error); } finally { cancellation.Dispose(); cancellation = null; SetBusy(false); }
    }
    private void SetBusy(bool busy) { foreach (Control c in Controls) c.Enabled = !busy; cancel.Enabled = busy; }
    private void UpdateStatus()
    {
        if (report is null) return; firmwareStatus.ForeColor = ColorFor(report.FirmwareStatus); windowsStatus.ForeColor = ColorFor(report.WindowsStatus); efiStatus.ForeColor = ColorFor(report.EfiStatus); environmentStatus.ForeColor = ColorFor(report.EnvironmentStatus);
        SetDetail(firmwareStatus, StatusText.ForFirmware(report.Firmware) + $" ({report.Firmware.Details})"); SetDetail(windowsStatus, report.SelectedWindows is null ? "No unambiguous Windows installation" : $"Windows installation found: {report.SelectedWindows.WindowsPath}"); SetDetail(efiStatus, report.SelectedEfi is null ? "No unambiguous EFI System Partition" : $"EFI System Partition found: {report.SelectedEfi.Volume.FileSystem}, {report.SelectedEfi.Volume.SizeBytes / (1024 * 1024)} MB, {(report.SelectedEfi.Volume.DriveLetter is null ? "no letter assigned" : "letter " + report.SelectedEfi.Volume.DriveLetter)}"); SetDetail(environmentStatus, report.WinPeDetails);
    }
    private static Color ColorFor(StatusLevel level) => level switch { StatusLevel.Ok => Color.Green, StatusLevel.Warning => Color.Goldenrod, StatusLevel.Error => Color.Red, _ => Color.Gray };
    private static void SetDetail(Label indicator, string text) { if (indicator.Parent is FlowLayoutPanel panel && panel.Controls.Count > 1 && panel.Controls[1] is Label label) label.Text = text; }
    private string BuildReport() => logger is BootRepairLogger b ? b.BuildReport(report, repairResult) : new StringBuilder().AppendLine(report?.RepairBlockReason).ToString();
}
