using BootRepairAssistant2.Core;

namespace BootRepairAssistant2;

internal sealed class ConfirmRepairDialog : Form
{
    private readonly TextBox confirmation = new() { Width = 180 };
    public ConfirmRepairDialog(DiagnosticReport report, string command, string backup, bool testMode)
    {
        Text = testMode ? "Confirm simulated repair" : "Confirm repair";
        MinimumSize = new Size(650, 420); StartPosition = FormStartPosition.CenterParent;
        var text = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Top, Height = 230, Font = new Font("Segoe UI", 12), Text = $"Detected firmware: {report.Firmware.Mode}\r\nWindows: {report.SelectedWindows?.WindowsPath}\r\nEFI: {report.SelectedEfi?.Volume.VolumeGuidPath}\r\nExact command: {command}\r\nBackup location: {backup}\r\n\r\n{(testMode ? "Test Mode will simulate this operation and make no changes." : "This will modify boot files after creating a backup.")}\r\nType REPAIR (case-sensitive) to enable OK." };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 65, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Enabled = false, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        confirmation.PlaceholderText = "Type REPAIR";
        confirmation.TextChanged += (_, _) => ok.Enabled = confirmation.Text == "REPAIR";
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        Controls.Add(text); Controls.Add(confirmation); Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel;
    }
}
