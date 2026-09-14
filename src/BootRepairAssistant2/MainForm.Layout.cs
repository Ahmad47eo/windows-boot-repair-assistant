namespace BootRepairAssistant2;

internal sealed partial class MainForm
{
    private readonly Label firmwareStatus = StatusLabel();
    private readonly Label windowsStatus = StatusLabel();
    private readonly Label efiStatus = StatusLabel();
    private readonly Label environmentStatus = StatusLabel();
    private readonly TextBox output = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 11)
    };
    private readonly CheckBox testMode = new()
    {
        Text = "Test Mode (no changes will be made)",
        Checked = true,
        AutoSize = true,
        Font = new Font("Segoe UI", 12)
    };
    private readonly Button scan = new() { Text = "Scan PC" };
    private readonly Button diagnose = new() { Text = "Diagnose Only" };
    private readonly Button repairButton = new() { Text = "Repair Boot" };
    private readonly Button verifyButton = new() { Text = "Verify Repair" };
    private readonly Button viewLog = new() { Text = "View Log" };
    private readonly Button copy = new() { Text = "Copy Report" };
    private readonly Button cancel = new()
    {
        Text = "Cancel",
        Enabled = false
    };

    private void InitializeLayout()
    {
        Text = "Windows Boot Repair Assistant 2";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 720);
        Width = 1200;
        Height = 820;
        Font = new Font("Segoe UI", 12);

        var title = new Label
        {
            Text = "Windows Boot Repair Assistant 2",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 50
        };
        var warning = new Label
        {
            Text = "Do not use Repair unless the diagnostic checks are all successful.",
            ForeColor = Color.DarkRed,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 32
        };
        var statuses = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            ColumnCount = 2,
            RowCount = 4
        };
        statuses.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 190));
        statuses.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));
        AddStatus(statuses, 0, "Firmware", firmwareStatus);
        AddStatus(statuses, 1, "Windows", windowsStatus);
        AddStatus(statuses, 2, "EFI", efiStatus);
        AddStatus(statuses, 3, "Environment", environmentStatus);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 95,
            WrapContents = true
        };
        foreach (var button in new[]
                 {
                     scan,
                     diagnose,
                     repairButton,
                     verifyButton,
                     viewLog,
                     copy,
                     cancel
                 })
        {
            button.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            button.AutoSize = true;
            actions.Controls.Add(button);
        }

        actions.Controls.Add(testMode);
        var outputPanel = new GroupBox
        {
            Text = "Detailed output and log",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        outputPanel.Controls.Add(output);

        Controls.Add(outputPanel);
        Controls.Add(actions);
        Controls.Add(statuses);
        Controls.Add(warning);
        Controls.Add(title);
        repairButton.Enabled = false;
        repairButton.Text = "Repair Boot (Test Mode – simulate)";
    }

    private static Label StatusLabel()
    {
        return new Label
        {
            Text = "●",
            AutoSize = true,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.Gray
        };
    }

    private static void AddStatus(
        TableLayoutPanel table,
        int row,
        string name,
        Label indicator)
    {
        table.RowStyles.Add(
            new RowStyle(SizeType.Percent, 25));
        var label = new Label
        {
            Text = name,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        panel.Controls.Add(indicator);
        panel.Controls.Add(new Label
        {
            Name = name + "Detail",
            AutoSize = true
        });
        table.Controls.Add(label, 0, row);
        table.Controls.Add(panel, 1, row);
    }
}
