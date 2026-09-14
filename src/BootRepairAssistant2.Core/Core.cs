using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace BootRepairAssistant2.Core;

public enum FirmwareMode { Unknown, LegacyBios, Uefi }
public record FirmwareDetectionResult(FirmwareMode Mode, string Source, string Details);
public enum StatusLevel { Ok, Warning, Error, Unknown }
public record WindowsInstallation(string DriveLetter, string WindowsPath, IReadOnlyList<string> MarkersFound, IReadOnlyList<string> MarkersMissing)
{
    public bool IsValid => MarkersMissing.Count == 0;
}
public record VolumeInfo(string? DriveLetter, string VolumeGuidPath, string? FileSystem, long SizeBytes, Guid? GptPartitionType, bool IsGpt, string? Label);
public record EfiPartitionCandidate(VolumeInfo Volume, int Score, IReadOnlyList<string> Reasons);
public enum RepairOutcome { Success, Warning, Failed, NotRun }
public record CommandResult(string FileName, IReadOnlyList<string> Arguments, int? ExitCode, string StdOut, string StdErr, bool TimedOut, bool Cancelled, TimeSpan Duration)
{
    public string DisplayCommand => string.Join(" ", new[] { FileName }.Concat(Arguments).Select(QuoteForDisplay));
    private static string QuoteForDisplay(string value) => value.Any(char.IsWhiteSpace) || value.Contains('"') ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
}
public record VerificationResult(RepairOutcome Outcome, IReadOnlyList<(string Check, bool Passed, string Detail)> Checks);
public record RepairResult(RepairOutcome Outcome, string BackupDirectory, CommandResult? BcdBoot, VerificationResult? Verification, DiagnosticReport? PostScan, string Summary);

public sealed class DiagnosticReport
{
    public FirmwareDetectionResult Firmware { get; init; } = new(FirmwareMode.Unknown, "None", "Not scanned");
    public bool IsWinPe { get; init; }
    public string WinPeDetails { get; init; } = "";
    public IReadOnlyList<WindowsInstallation> WindowsCandidates { get; init; } = Array.Empty<WindowsInstallation>();
    public WindowsInstallation? SelectedWindows { get; init; }
    public IReadOnlyList<EfiPartitionCandidate> EfiCandidates { get; init; } = Array.Empty<EfiPartitionCandidate>();
    public EfiPartitionCandidate? SelectedEfi { get; init; }
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public bool RepairAllowed
    {
        get
        {
            if (Firmware.Mode != FirmwareMode.Uefi || WindowsCandidates.Count(x => x.IsValid) != 1 || EfiCandidates.Count == 0) return false;
            var top = EfiCandidates.Where(x => x.Score >= 30).OrderByDescending(x => x.Score).ToList();
            return top.Count == 1 && SelectedEfi is not null && ReferenceEquals(SelectedEfi, top[0]);
        }
    }
    public string RepairBlockReason
    {
        get
        {
            if (Firmware.Mode != FirmwareMode.Uefi) return "Repair requires UEFI firmware; legacy BIOS or unknown firmware was detected.";
            var windows = WindowsCandidates.Count(x => x.IsValid);
            if (windows != 1) return windows == 0 ? "No unambiguous valid Windows installation was found." : "Multiple valid Windows installations were found.";
            var top = EfiCandidates.Where(x => x.Score >= 30).OrderByDescending(x => x.Score).ToList();
            if (top.Count == 0) return "No sufficiently trustworthy EFI System Partition candidate was found.";
            if (top.Count > 1 && top[0].Score == top[1].Score) return "EFI System Partition selection is ambiguous because top candidates tie.";
            if (SelectedEfi is null) return "No EFI System Partition was selected.";
            if (!ReferenceEquals(SelectedEfi, top[0])) return "Selected EFI System Partition is not the highest-confidence candidate.";
            return "";
        }
    }
    public StatusLevel FirmwareStatus => Firmware.Mode == FirmwareMode.Uefi ? StatusLevel.Ok : Firmware.Mode == FirmwareMode.Unknown ? StatusLevel.Unknown : StatusLevel.Warning;
    public StatusLevel WindowsStatus => WindowsCandidates.Count(x => x.IsValid) == 1 ? StatusLevel.Ok : WindowsCandidates.Any(x => x.IsValid) ? StatusLevel.Warning : StatusLevel.Error;
    public StatusLevel EfiStatus => RepairAllowed ? StatusLevel.Ok : EfiCandidates.Count > 0 ? StatusLevel.Warning : StatusLevel.Error;
    public StatusLevel EnvironmentStatus => IsWinPe ? StatusLevel.Ok : StatusLevel.Warning;
}

public interface IRegistryReader
{
    object? GetValue(string hiveRelativeKeyPath, string valueName);
    bool KeyExists(string hiveRelativeKeyPath);
}
public interface IFirmwareApi { uint? GetFirmwareType(); }
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string dir, string pattern);
    void CreateDirectory(string path);
    void CopyFile(string src, string dst, bool overwrite);
    long GetFileLength(string path);
}
public interface IVolumeProvider { IReadOnlyList<VolumeInfo> GetVolumes(); }
public interface IVolumeMounter
{
    string? AssignLetter(string volumeGuidPath);
    void RemoveLetter(string letter);
}
public interface IProcessRunner { Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct); }
public interface IEnvironmentInfo { bool IsWindows { get; } string SystemRoot { get; } string? GetEnvVar(string name); }
public interface ILogSink { void Log(string message); }

public sealed class WindowsRegistryReader : IRegistryReader
{
    public object? GetValue(string hiveRelativeKeyPath, string valueName)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try { using var key = Registry.LocalMachine.OpenSubKey(hiveRelativeKeyPath); return key?.GetValue(valueName); } catch { return null; }
    }
    public bool KeyExists(string hiveRelativeKeyPath)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { using var key = Registry.LocalMachine.OpenSubKey(hiveRelativeKeyPath); return key is not null; } catch { return false; }
    }
}

public sealed class WindowsFirmwareApi : IFirmwareApi
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFirmwareType(out uint firmwareType);
    public uint? GetFirmwareType()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try { return GetFirmwareType(out var value) ? value : null; } catch (DllNotFoundException) { return null; } catch (EntryPointNotFoundException) { return null; }
    }
}

public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateFiles(string dir, string pattern) => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern) : Array.Empty<string>();
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void CopyFile(string src, string dst, bool overwrite) => File.Copy(src, dst, overwrite);
    public long GetFileLength(string path) => new FileInfo(path).Length;
}

public sealed class SystemEnvironmentInfo : IEnvironmentInfo
{
    public bool IsWindows => OperatingSystem.IsWindows();
    public string SystemRoot => Environment.GetEnvironmentVariable("SystemRoot") ?? "";
    public string? GetEnvVar(string name) => Environment.GetEnvironmentVariable(name);
}

public sealed class WindowsVolumeProvider : IVolumeProvider
{
    public IReadOnlyList<VolumeInfo> GetVolumes()
    {
        var result = new List<VolumeInfo>();
        if (!OperatingSystem.IsWindows()) return result;
        var nameBuffer = new StringBuilder(1024);
        var handle = FindFirstVolume(nameBuffer, nameBuffer.Capacity);
        if (handle == InvalidHandleValue) return result;
        try
        {
            do
            {
                AddVolume(result, nameBuffer.ToString());
                nameBuffer.Clear();
            } while (FindNextVolume(handle, nameBuffer, nameBuffer.Capacity));
        }
        catch { }
        finally { FindVolumeClose(handle); }
        return result;
    }
    private static void AddVolume(List<VolumeInfo> result, string guidPath)
    {
        try
        {
            var pathBuffer = new StringBuilder(1024);
            GetVolumePathNamesForVolumeName(guidPath, pathBuffer, pathBuffer.Capacity, out _);
            var letter = pathBuffer.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimEnd('\\').TrimEnd(':').ToUpperInvariant();
            var fsBuffer = new StringBuilder(64); var labelBuffer = new StringBuilder(256);
            GetVolumeInformation(guidPath, labelBuffer, labelBuffer.Capacity, out _, out _, out _, fsBuffer, fsBuffer.Capacity);
            var partition = GetPartitionInfo(guidPath);
            var size = partition.SizeBytes;
            if (size == 0 && letter is not null) { var drive = new DriveInfo(letter + ":"); if (drive.IsReady) size = drive.TotalSize; }
            result.Add(new VolumeInfo(letter, guidPath, string.IsNullOrWhiteSpace(fsBuffer.ToString()) ? null : fsBuffer.ToString(), size, partition.PartitionType, partition.IsGpt, labelBuffer.ToString()));
        }
        catch { }
    }
    private static (bool IsGpt, Guid? PartitionType, long SizeBytes) GetPartitionInfo(string path)
    {
        using var handle = File.OpenHandle(path.TrimEnd('\\'), FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.None);
        var buffer = new byte[128]; if (!DeviceIoControl(handle.DangerousGetHandle(), IoctlDiskGetPartitionInfoEx, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero)) return (false, null, 0);
        var style = BitConverter.ToInt32(buffer, 0); var size = BitConverter.ToInt64(buffer, 16);
        return style == 1 ? (true, new Guid(buffer.AsSpan(32, 16)), size) : (false, null, size);
    }
    private const uint IoctlDiskGetPartitionInfoEx = 0x00070048;
    private static readonly IntPtr InvalidHandleValue = new(-1);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstVolume(StringBuilder volumeName, int volumeNameSize);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool FindNextVolume(IntPtr handle, StringBuilder volumeName, int volumeNameSize);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindVolumeClose(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumePathNamesForVolumeName(string volumeName, StringBuilder volumePathNames, int bufferLength, out int returnLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(string rootPathName, StringBuilder volumeName, int volumeNameSize, out uint serialNumber, out uint maxComponentLength, out uint fileSystemFlags, StringBuilder fileSystemName, int fileSystemNameSize);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr device, uint code, byte[] input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);
}

public sealed class WindowsVolumeMounter : IVolumeMounter
{
    public string? AssignLetter(string volumeGuidPath)
    {
        if (!OperatingSystem.IsWindows()) return null;
        for (var c = 'Z'; c >= 'D'; c--)
        {
            var letter = c + @":\";
            if (Directory.Exists(letter)) continue;
            if (SetVolumeMountPoint(letter, volumeGuidPath.EndsWith("\\", StringComparison.Ordinal) ? volumeGuidPath : volumeGuidPath + "\\")) return c.ToString();
        }
        return null;
    }
    public void RemoveLetter(string letter) { if (OperatingSystem.IsWindows()) DeleteVolumeMountPoint(letter.TrimEnd(':') + @":\"); }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetVolumeMountPoint(string mountPoint, string volumeName);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteVolumeMountPoint(string mountPoint);
}

public sealed class FirmwareDetector
{
    private readonly IRegistryReader registry; private readonly IFirmwareApi api; private readonly ILogSink log;
    public FirmwareDetector(IRegistryReader registry, IFirmwareApi api, ILogSink? log = null) { this.registry = registry; this.api = api; this.log = log ?? NullLogSink.Instance; }
    public FirmwareDetectionResult Detect()
    {
        var raw = registry.GetValue(@"SYSTEM\CurrentControlSet\Control", "PEFirmwareType");
        var result = Interpret(raw);
        if (result.Mode != FirmwareMode.Unknown) { log.Log($"Firmware: {result.Details}"); return result; }
        var apiValue = api.GetFirmwareType();
        var apiResult = InterpretApi(apiValue);
        if (apiResult.Mode != FirmwareMode.Unknown) { var withSource = apiResult with { Source = "GetFirmwareType API", Details = $"{apiResult.Details}; registry {result.Details}" }; log.Log($"Firmware: {withSource.Details}"); return withSource; }
        var details = $"{result.Details}; GetFirmwareType API unavailable or returned an unexpected value ({(apiValue.HasValue ? apiValue.Value.ToString() : "missing")}).";
        return new(FirmwareMode.Unknown, "None", details);
    }
    public static FirmwareDetectionResult Interpret(object? rawValue)
    {
        if (rawValue is int i) return FromValue(i, "Registry PEFirmwareType");
        if (rawValue is uint u) return FromValue(u, "Registry PEFirmwareType");
        if (rawValue is long l) return FromValue(l, "Registry PEFirmwareType");
        return new(FirmwareMode.Unknown, "Registry PEFirmwareType", rawValue is null ? "PEFirmwareType value missing" : $"unexpected value type {rawValue.GetType().Name}");
    }
    private static FirmwareDetectionResult InterpretApi(uint? value) => value.HasValue ? FromValue(value.Value, "GetFirmwareType API") : new(FirmwareMode.Unknown, "GetFirmwareType API", "API unavailable");
    private static FirmwareDetectionResult FromValue(long value, string source) => value switch { 1 => new(FirmwareMode.LegacyBios, source, $"{source} = 1 (BIOS)"), 2 => new(FirmwareMode.Uefi, source, $"{source} = 2 (UEFI)"), _ => new(FirmwareMode.Unknown, source, $"{source} unexpected value {value}") };
}

public sealed class WinPeDetector
{
    private readonly IRegistryReader registry; private readonly IEnvironmentInfo environment;
    public WinPeDetector(IRegistryReader registry, IEnvironmentInfo environment) { this.registry = registry; this.environment = environment; }
    public (bool IsWinPe, string Details) Detect()
    {
        var miniNt = registry.KeyExists(@"SYSTEM\CurrentControlSet\Control\MiniNT");
        var root = environment.SystemRoot.StartsWith(@"X:\", StringComparison.OrdinalIgnoreCase);
        return (miniNt || root, miniNt && root ? "MiniNT registry key and SystemRoot X:\\ evidence" : miniNt ? "MiniNT registry key detected" : root ? "SystemRoot starts with X:\\ (WinPE ramdisk)" : "No WinPE evidence detected");
    }
}

public sealed class WindowsInstallationFinder
{
    private readonly IVolumeProvider volumes; private readonly IFileSystem fs; private readonly ILogSink log;
    public WindowsInstallationFinder(IVolumeProvider volumes, IFileSystem fs, ILogSink? log = null) { this.volumes = volumes; this.fs = fs; this.log = log ?? NullLogSink.Instance; }
    public IReadOnlyList<WindowsInstallation> Find(string? winPeDrive = null)
    {
        var result = new List<WindowsInstallation>();
        foreach (var volume in volumes.GetVolumes().Where(x => !string.IsNullOrWhiteSpace(x.DriveLetter)))
        {
            var drive = volume.DriveLetter!.TrimEnd(':').ToUpperInvariant();
            if (string.Equals(drive, (winPeDrive ?? "X").TrimEnd(':').ToUpperInvariant(), StringComparison.OrdinalIgnoreCase)) { log.Log($"Skipping {drive}: because it is the WinPE ramdisk."); continue; }
            var root = drive + @":\Windows";
            var checks = new (string Relative, bool IsDirectory)[] { (@"System32\", true), (@"System32\ntoskrnl.exe", false), (@"System32\config\SYSTEM", false), (@"System32\winload.efi", false), (@"explorer.exe", false), (@"Boot\EFI\bootmgfw.efi", false) };
            var found = checks.Where(x => x.IsDirectory ? fs.DirectoryExists(Path.Combine(drive + @":\Windows", x.Relative)) : fs.FileExists(Path.Combine(drive + @":\Windows", x.Relative))).Select(x => x.Relative).ToList();
            var required = checks.Take(4).ToArray();
            var missing = required.Where(x => !(x.IsDirectory ? fs.DirectoryExists(Path.Combine(drive + @":\Windows", x.Relative)) : fs.FileExists(Path.Combine(drive + @":\Windows", x.Relative)))).Select(x => x.Relative).ToList();
            result.Add(new WindowsInstallation(drive, root, found, missing));
        }
        return result;
    }
}

public sealed class EfiPartitionFinder
{
    public static readonly Guid EspType = Guid.Parse("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");
    private readonly IVolumeProvider volumes; private readonly IFileSystem fs; private readonly ILogSink log; private readonly string? windowsDrive;
    public EfiPartitionFinder(IVolumeProvider volumes, IFileSystem fs, ILogSink? log = null, string? windowsDrive = null) { this.volumes = volumes; this.fs = fs; this.log = log ?? NullLogSink.Instance; this.windowsDrive = windowsDrive?.TrimEnd(':').ToUpperInvariant(); }
    public IReadOnlyList<EfiPartitionCandidate> Find(string? windowsDriveOverride = null)
    {
        var candidates = new List<EfiPartitionCandidate>();
        var excludedWindowsDrive = windowsDriveOverride?.TrimEnd(':').ToUpperInvariant() ?? windowsDrive;
        foreach (var volume in volumes.GetVolumes())
        {
            var score = 0; var reasons = new List<string>(); var drive = volume.DriveLetter?.TrimEnd(':').ToUpperInvariant();
            if (volume.GptPartitionType == EspType) { score += 50; reasons.Add("GPT EFI System Partition type"); }
            if (string.Equals(volume.FileSystem, "FAT32", StringComparison.OrdinalIgnoreCase) || string.Equals(volume.FileSystem, "FAT", StringComparison.OrdinalIgnoreCase)) { score += 20; reasons.Add("FAT filesystem"); }
            if (volume.SizeBytes is >= 32L * 1024 * 1024 and <= 2L * 1024 * 1024 * 1024) { score += 10; reasons.Add("size is typical for ESP"); }
            var root = drive is null ? volume.VolumeGuidPath : drive + @":\";
            if (fs.DirectoryExists(Path.Combine(root, @"EFI\Microsoft\Boot"))) { score += 15; reasons.Add("Microsoft EFI boot directory"); }
            if (fs.FileExists(Path.Combine(root, @"EFI\Boot\bootx64.efi"))) { score += 5; reasons.Add("fallback bootx64.efi"); }
            if (string.Equals(volume.FileSystem, "NTFS", StringComparison.OrdinalIgnoreCase)) { score -= 100; reasons.Add("NTFS is not an EFI filesystem"); }
            if (excludedWindowsDrive is not null && drive == excludedWindowsDrive) { score -= 100; reasons.Add("Windows installation volume"); }
            if (volume.SizeBytes > 4L * 1024 * 1024 * 1024) { score -= 50; reasons.Add("volume is larger than 4 GB"); }
            if (score >= 30) candidates.Add(new(volume, score, reasons));
        }
        return candidates.OrderByDescending(x => x.Score).ToList();
    }
}

public sealed class Diagnostics
{
    private readonly FirmwareDetector firmware; private readonly WinPeDetector winPe; private readonly WindowsInstallationFinder windows; private readonly EfiPartitionFinder efi; private readonly ILogSink log;
    public Diagnostics(FirmwareDetector firmware, WinPeDetector winPe, WindowsInstallationFinder windows, EfiPartitionFinder efi, ILogSink? log = null) { this.firmware = firmware; this.winPe = winPe; this.windows = windows; this.efi = efi; this.log = log ?? NullLogSink.Instance; }
    public Task<DiagnosticReport> RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fw = firmware.Detect(); var pe = winPe.Detect(); var win = windows.Find(pe.IsWinPe ? "X" : null); var valid = win.Where(x => x.IsValid).ToList();
        var selectedWin = valid.Count == 1 ? valid[0] : null; var efi = this.efi.Find(selectedWin?.DriveLetter); var top = efi.OrderByDescending(x => x.Score).ToList(); var selectedEfi = top.Count > 0 ? top[0] : null;
        var problems = new List<string>();
        if (fw.Mode != FirmwareMode.Uefi) problems.Add($"Firmware is {fw.Mode}; UEFI is required for this repair. Safe to continue: diagnostics only.");
        if (valid.Count == 0) problems.Add("No valid Windows installation was found; required system markers are missing. Repair is blocked.");
        if (valid.Count > 1) problems.Add("Multiple valid Windows installations were found; selecting one would be unsafe.");
        if (top.Count == 0) problems.Add("No EFI candidate met the confidence threshold. Repair is blocked.");
        else if (top.Count > 1 && top[0].Score == top[1].Score) { selectedEfi = null; problems.Add("Top EFI candidates have the same score, so the EFI partition is ambiguous."); }
        else if (top[0].Volume.GptPartitionType != EfiPartitionFinder.EspType && !top[0].Reasons.Contains("Microsoft EFI boot directory")) problems.Add("The top EFI candidate is not GPT ESP-typed and lacks a Microsoft boot directory; repair is blocked.");
        if (!pe.IsWinPe) problems.Add("Windows PE was not detected. Read-only diagnostics are safe; repair is intended for WinPE.");
        var report = new DiagnosticReport { Firmware = fw, IsWinPe = pe.IsWinPe, WinPeDetails = pe.Details, WindowsCandidates = win, SelectedWindows = selectedWin, EfiCandidates = efi, SelectedEfi = selectedEfi, Problems = problems };
        log.Log($"Diagnostics completed: RepairAllowed={report.RepairAllowed}; {report.Problems.Count} problem(s)."); return Task.FromResult(report);
    }
}

public sealed class BackupManager
{
    private readonly IFileSystem fs; private readonly ILogSink log;
    public BackupManager(IFileSystem fs, ILogSink? log = null) { this.fs = fs; this.log = log ?? NullLogSink.Instance; }
    public string CreateBackup(DiagnosticReport report)
    {
        var windowsDrive = report.SelectedWindows?.DriveLetter ?? throw new InvalidOperationException("A selected Windows installation is required for backup.");
        var efi = report.SelectedEfi?.Volume.DriveLetter?.TrimEnd(':');
        var bases = new[] { $"{windowsDrive}:\\BootRepairAssistant2\\Backups", Path.Combine(AppContext.BaseDirectory, "Backups"), Path.Combine(Path.GetTempPath(), "BootRepairAssistant2", "Backups") };
        string? basePath = bases.FirstOrDefault(IsWritable);
        if (basePath is null) throw new IOException("No writable backup location was found.");
        fs.CreateDirectory(basePath);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); var path = Path.Combine(basePath, stamp); var n = 1;
        while (fs.DirectoryExists(path) || Directory.Exists(path)) path = Path.Combine(basePath, $"{stamp}_{++n}");
        fs.CreateDirectory(path);
        var info = Path.Combine(path, "backup-info.txt"); var text = $"Backup created: {DateTime.Now:u}\nWindows: {report.SelectedWindows.WindowsPath}\nEFI: {report.SelectedEfi?.Volume.VolumeGuidPath}\nFirmware: {report.Firmware.Mode}\n";
        var sourceRoot = efi is null ? null : efi + @":\EFI\Microsoft\Boot";
        var copied = false;
        if (sourceRoot is not null && fs.FileExists(Path.Combine(sourceRoot, "BCD"))) { fs.CopyFile(Path.Combine(sourceRoot, "BCD"), Path.Combine(path, "BCD"), false); copied = true; foreach (var file in fs.EnumerateFiles(sourceRoot, "BCD.LOG*")) fs.CopyFile(file, Path.Combine(path, Path.GetFileName(file)), false); }
        if (!copied) text += "Note: no existing BCD to back up.\n";
        File.WriteAllText(info, text);
        log.Log($"Backup created at {path}"); return path;
    }
    private static bool IsWritable(string directory)
    {
        if (!OperatingSystem.IsWindows() && Regex.IsMatch(directory, @"^[A-Za-z]:\\", RegexOptions.CultureInvariant)) return false;
        try { Directory.CreateDirectory(directory); var probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N")); File.WriteAllText(probe, ""); File.Delete(probe); return true; } catch { return false; }
    }
}

public static class BcdBootCommandBuilder
{
    private static readonly Regex Letter = new("^[A-Za-z]$", RegexOptions.Compiled);
    public static (string FileName, IReadOnlyList<string> Arguments) Build(string windowsPath, string efiLetter)
    {
        if (!Letter.IsMatch(efiLetter)) throw new ArgumentException("EFI drive letter must be a single A-Z character.", nameof(efiLetter));
        if (windowsPath.Contains('"') || windowsPath.Contains('&') || windowsPath.Contains('|') || windowsPath.Contains(';')) throw new ArgumentException("Windows path contains unsafe command characters.", nameof(windowsPath));
        return ("bcdboot", new[] { windowsPath, "/s", efiLetter.TrimEnd(':') + ":", "/f", "UEFI" });
    }
}

public sealed class Verifier
{
    private readonly IFileSystem fs; public Verifier(IFileSystem fs) => this.fs = fs;
    public VerificationResult Verify(string efiLetter)
    {
        if (!Regex.IsMatch(efiLetter, "^[A-Za-z]$")) throw new ArgumentException("EFI drive letter must be a single character.", nameof(efiLetter));
        var root = efiLetter.ToUpperInvariant() + @":\EFI\Microsoft\Boot"; var checks = new List<(string, bool, string)>();
        bool Required(string name) { var ok = fs.FileExists(Path.Combine(root, name)); checks.Add((name, ok, ok ? "present" : "missing (required)")); return ok; }
        var bootmgfw = Required("bootmgfw.efi"); var bcd = Required("BCD"); var bootmgr = fs.FileExists(Path.Combine(root, "bootmgr.efi")); checks.Add(("bootmgr.efi", bootmgr, bootmgr ? "present" : "missing (optional)"));
        var fallback = fs.FileExists(efiLetter.ToUpperInvariant() + @":\EFI\Boot\bootx64.efi"); checks.Add(("bootx64.efi", fallback, fallback ? "present" : "missing (optional)"));
        var nonEmpty = bcd && fs.GetFileLength(Path.Combine(root, "BCD")) > 0; checks.Add(("BCD non-empty", nonEmpty, nonEmpty ? "size is greater than zero" : "empty or missing"));
        var required = bootmgfw && bcd && nonEmpty; var optional = bootmgr && fallback;
        return new(required ? optional ? RepairOutcome.Success : RepairOutcome.Warning : RepairOutcome.Failed, checks);
    }
}

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly Regex SafeName = new(@"^[A-Za-z0-9_.:/\\\- ]+$", RegexOptions.Compiled);
    public async Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct)
    {
        if (!SafeName.IsMatch(fileName)) throw new ArgumentException("Executable path contains unsupported characters.", nameof(fileName));
        var start = Stopwatch.StartNew(); using var process = new Process { StartInfo = new ProcessStartInfo { FileName = fileName, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        try { process.Start(); } catch { start.Stop(); throw; }
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var timedOut = false; var cancelled = false;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try { await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false); } catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested; cancelled = ct.IsCancellationRequested;
            try { process.Kill(true); } catch { }
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        start.Stop(); return new(fileName, args, timedOut || cancelled ? null : process.ExitCode, await stdout, await stderr, timedOut, cancelled, start.Elapsed);
    }
}

public sealed class RepairEngine
{
    private readonly IProcessRunner runner; private readonly BackupManager backup; private readonly Verifier verifier; private readonly Diagnostics diagnostics; private readonly FirmwareDetector firmware; private readonly IVolumeMounter mounter; private readonly IFileSystem fs; private readonly ILogSink log;
    public RepairEngine(IProcessRunner runner, BackupManager backup, Verifier verifier, Diagnostics diagnostics, FirmwareDetector firmware, IFileSystem fs, ILogSink? log = null) : this(runner, backup, verifier, diagnostics, firmware, new WindowsVolumeMounter(), fs, log) { }
    public RepairEngine(IProcessRunner runner, BackupManager backup, Verifier verifier, Diagnostics diagnostics, FirmwareDetector firmware, IVolumeMounter mounter, IFileSystem fs, ILogSink? log = null) { this.runner = runner; this.backup = backup; this.verifier = verifier; this.diagnostics = diagnostics; this.firmware = firmware; this.mounter = mounter; this.fs = fs; this.log = log ?? NullLogSink.Instance; }
    public async Task<RepairResult> RepairAsync(DiagnosticReport report, bool testMode, IProgress<string>? progress, CancellationToken ct)
    {
        if (!report.RepairAllowed) return new(RepairOutcome.Failed, "", null, null, null, report.RepairBlockReason);
        if (ct.IsCancellationRequested) return new(RepairOutcome.NotRun, "", null, null, null, "Repair cancelled before starting.");
        if (firmware.Detect().Mode != FirmwareMode.Uefi) return new(RepairOutcome.Failed, "", null, null, null, "Firmware changed or UEFI is no longer detected.");
        var backupDir = backup.CreateBackup(report); progress?.Report($"Backup created: {backupDir}");
        var efiLetter = report.SelectedEfi!.Volume.DriveLetter?.TrimEnd(':'); var assignedLetter = false;
        if (efiLetter is null)
        {
            if (testMode) return new(RepairOutcome.NotRun, backupDir, null, null, null, "EFI has no drive letter. Test Mode reports this without making mount changes.");
            efiLetter = mounter.AssignLetter(report.SelectedEfi.Volume.VolumeGuidPath);
            if (efiLetter is null) return new(RepairOutcome.Failed, backupDir, null, null, null, "EFI has no drive letter and no free temporary drive letter could be assigned.");
            assignedLetter = true; log.Log($"Temporarily assigned EFI volume {report.SelectedEfi.Volume.VolumeGuidPath} to {efiLetter}:");
        }
        var windowsPath = report.SelectedWindows!.WindowsPath; var command = BcdBootCommandBuilder.Build(windowsPath, efiLetter); var exe = Path.Combine(AppContext.BaseDirectory, "bcdboot.exe"); var fileName = fs.FileExists(exe) ? exe : command.FileName;
        if (testMode) return new(RepairOutcome.NotRun, backupDir, new(fileName, command.Arguments, null, "", "", false, false, TimeSpan.Zero), null, null, $"Test Mode: would run {new CommandResult(fileName, command.Arguments, null, "", "", false, false, TimeSpan.Zero).DisplayCommand}");
        if (ct.IsCancellationRequested) return new(RepairOutcome.NotRun, backupDir, null, null, null, "Repair cancelled before bcdboot.");
        try
        {
            var result = await runner.RunAsync(fileName, command.Arguments, TimeSpan.FromSeconds(120), CancellationToken.None);
            progress?.Report("bcdboot completed."); var verification = verifier.Verify(efiLetter);
            var post = await diagnostics.RunAsync(CancellationToken.None); var outcome = result.ExitCode == 0 ? verification.Outcome : RepairOutcome.Failed;
            return new(outcome, backupDir, result, verification, post, result.ExitCode == 0 ? $"bcdboot completed with exit code 0; verification {verification.Outcome}." : $"bcdboot failed with exit code {result.ExitCode}: {result.StdErr}");
        }
        finally
        {
            if (assignedLetter) { mounter.RemoveLetter(efiLetter); log.Log($"Removed temporary EFI drive letter {efiLetter}:"); }
        }
    }
}

public sealed class BootRepairLogger : ILogSink
{
    private readonly List<string> entries = new(); private readonly string path;
    public BootRepairLogger(IFileSystem? fs = null)
    {
        var dir = AppContext.BaseDirectory; try { Directory.CreateDirectory(dir); path = Path.Combine(dir, $"BootRepairAssistant2_{DateTime.Now:yyyyMMdd_HHmmss}.log"); } catch { path = Path.Combine(Path.GetTempPath(), $"BootRepairAssistant2_{DateTime.Now:yyyyMMdd_HHmmss}.log"); }
    }
    public void Log(string message) { var line = $"{DateTime.Now:u} {message}"; lock (entries) entries.Add(line); try { File.AppendAllText(path, line + Environment.NewLine); } catch { } }
    public string BuildReport(DiagnosticReport? report, RepairResult? repair)
    {
        var sb = new StringBuilder(); sb.AppendLine("Windows Boot Repair Assistant 2 report"); sb.AppendLine($"Generated: {DateTime.Now:u}");
        if (report is not null) { sb.AppendLine($"Firmware: {report.Firmware.Mode} ({report.Firmware.Details})"); sb.AppendLine($"WinPE: {report.IsWinPe} ({report.WinPeDetails})"); sb.AppendLine($"Windows: {report.SelectedWindows?.WindowsPath ?? "none"}"); sb.AppendLine($"EFI: {report.SelectedEfi?.Volume.VolumeGuidPath ?? "none"}"); sb.AppendLine($"Repair allowed: {report.RepairAllowed}; {report.RepairBlockReason}"); foreach (var p in report.Problems) sb.AppendLine($"Problem: {p}"); }
        if (repair is not null) { sb.AppendLine($"Outcome: {repair.Outcome}"); sb.AppendLine($"Backup: {repair.BackupDirectory}"); sb.AppendLine($"Command: {repair.BcdBoot?.DisplayCommand ?? "none"}"); sb.AppendLine($"Exit code: {repair.BcdBoot?.ExitCode?.ToString() ?? "none"}"); sb.AppendLine($"Output: {repair.BcdBoot?.StdOut}"); sb.AppendLine($"Error: {repair.BcdBoot?.StdErr}"); sb.AppendLine($"Summary: {repair.Summary}"); if (repair.Verification is not null) foreach (var c in repair.Verification.Checks) sb.AppendLine($"Check {c.Check}: {(c.Passed ? "PASS" : "FAIL")} ({c.Detail})"); }
        lock (entries) { sb.AppendLine("Log:"); foreach (var line in entries) sb.AppendLine(line); } return sb.ToString();
    }
}

public static class StatusText
{
    public static string ForFirmware(FirmwareDetectionResult result) => result.Mode switch { FirmwareMode.Uefi => "UEFI detected", FirmwareMode.LegacyBios => "Legacy BIOS detected", _ => "Firmware unknown" };
}
internal sealed class NullLogSink : ILogSink
{
    public static readonly NullLogSink Instance = new(); public void Log(string message) { }
}
