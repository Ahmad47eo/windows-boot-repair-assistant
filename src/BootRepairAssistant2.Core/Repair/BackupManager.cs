namespace BootRepairAssistant2.Core;

public sealed class BackupManager
{
    private readonly IFileSystem fileSystem;
    private readonly ILogSink log;

    public BackupManager(
        IFileSystem fileSystem,
        ILogSink? log = null)
    {
        this.fileSystem = fileSystem;
        this.log = log ?? NullLogSink.Instance;
    }

    public string ResolveBackupBase(DiagnosticReport report)
    {
        var bases = new List<string>();
        if (report.SelectedWindows?.DriveLetter is string windowsDrive)
        {
            bases.Add(
                $"{windowsDrive.TrimEnd(':')}:\\BootRepairAssistant2\\Backups");
        }

        bases.Add(Path.Combine(AppContext.BaseDirectory, "Backups"));
        bases.Add(
            Path.Combine(Path.GetTempPath(), "BootRepairAssistant2", "Backups"));

        var basePath = bases.FirstOrDefault(IsWritable);
        if (basePath is null)
        {
            throw new IOException("No writable backup location was found.");
        }

        fileSystem.CreateDirectory(basePath);
        return basePath;
    }

    public string CreateBackup(DiagnosticReport report)
    {
        var basePath = ResolveBackupBase(report);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(basePath, stamp);
        var suffix = 1;
        while (fileSystem.DirectoryExists(path))
        {
            suffix++;
            path = Path.Combine(basePath, $"{stamp}_{suffix}");
        }

        fileSystem.CreateDirectory(path);
        var infoPath = Path.Combine(path, "backup-info.txt");
        var text =
            $"Backup created: {DateTime.Now:u}{Environment.NewLine}" +
            $"Windows: {report.SelectedWindows?.WindowsPath}{Environment.NewLine}" +
            $"EFI: {report.SelectedEfi?.Volume.VolumeGuidPath}{Environment.NewLine}" +
            $"Firmware: {report.Firmware.Mode}{Environment.NewLine}";

        var efiLetter = report.SelectedEfi?.Volume.DriveLetter?
            .TrimEnd(':');
        var sourceRoot = efiLetter is null
            ? null
            : efiLetter + @":\EFI\Microsoft\Boot";
        var copied = false;
        if (sourceRoot is not null
            && fileSystem.FileExists(Path.Combine(sourceRoot, "BCD")))
        {
            fileSystem.CopyFile(
                Path.Combine(sourceRoot, "BCD"),
                Path.Combine(path, "BCD"),
                false);
            copied = true;
            foreach (var file in fileSystem.EnumerateFiles(
                         sourceRoot,
                         "BCD.LOG*"))
            {
                fileSystem.CopyFile(
                    file,
                    Path.Combine(path, Path.GetFileName(file)),
                    false);
            }
        }

        if (!copied)
        {
            text += $"Note: no existing BCD to back up.{Environment.NewLine}";
        }

        fileSystem.WriteAllText(infoPath, text);
        log.Log($"Backup created at {path}");
        return path;
    }

    private bool IsWritable(string directory)
    {
        if (!OperatingSystem.IsWindows()
            && directory.Length >= 3
            && char.IsLetter(directory[0])
            && directory[1..3] == @":\")
        {
            return false;
        }

        try
        {
            fileSystem.CreateDirectory(directory);
            var probe = Path.Combine(
                directory,
                ".write-test-" + Guid.NewGuid().ToString("N"));
            fileSystem.WriteAllText(probe, string.Empty);
            fileSystem.DeleteFile(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
