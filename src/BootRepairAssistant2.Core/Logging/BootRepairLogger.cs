using System.Text;

namespace BootRepairAssistant2.Core;

public sealed class BootRepairLogger : ILogSink
{
    private readonly List<string> entries = new();
    private readonly string path;

    public BootRepairLogger()
    {
        var directory = AppContext.BaseDirectory;
        try
        {
            Directory.CreateDirectory(directory);
            path = Path.Combine(
                directory,
                $"BootRepairAssistant2_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }
        catch
        {
            path = Path.Combine(
                Path.GetTempPath(),
                $"BootRepairAssistant2_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }
    }

    public void Log(string message)
    {
        var line = $"{DateTime.Now:u} {message}";
        lock (entries)
        {
            entries.Add(line);
        }

        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    public string GetLogText()
    {
        lock (entries)
        {
            return string.Join(Environment.NewLine, entries);
        }
    }

    public string BuildReport(
        DiagnosticReport? report,
        RepairResult? repair)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Windows Boot Repair Assistant 2 report");
        builder.AppendLine($"Generated: {DateTime.Now:u}");
        if (report is not null)
        {
            builder.AppendLine(
                $"Firmware: {report.Firmware.Mode} ({report.Firmware.Details})");
            builder.AppendLine(
                $"WinPE: {report.IsWinPe} ({report.WinPeDetails})");
            builder.AppendLine(
                $"Windows: {report.SelectedWindows?.WindowsPath ?? "none"}");
            builder.AppendLine(
                $"EFI: {report.SelectedEfi?.Volume.VolumeGuidPath ?? "none"}");
            builder.AppendLine(
                $"Repair allowed: {report.RepairAllowed}; {report.RepairBlockReason}");
            foreach (var problem in report.Problems)
            {
                builder.AppendLine($"Problem: {problem}");
            }
        }

        if (repair is not null)
        {
            builder.AppendLine($"Outcome: {repair.Outcome}");
            builder.AppendLine($"Backup: {repair.BackupDirectory}");
            builder.AppendLine(
                $"Command: {repair.BcdBoot?.DisplayCommand ?? "none"}");
            builder.AppendLine(
                $"Exit code: {repair.BcdBoot?.ExitCode?.ToString() ?? "none"}");
            builder.AppendLine($"Output: {repair.BcdBoot?.StdOut}");
            builder.AppendLine($"Error: {repair.BcdBoot?.StdErr}");
            builder.AppendLine($"Summary: {repair.Summary}");
            if (repair.Verification is not null)
            {
                foreach (var check in repair.Verification.Checks)
                {
                    builder.AppendLine(
                        $"Check {check.Check}: {(check.Passed ? "PASS" : "FAIL")} ({check.Detail})");
                }
            }
        }

        builder.AppendLine("Log:");
        builder.AppendLine(GetLogText());
        return builder.ToString();
    }
}
