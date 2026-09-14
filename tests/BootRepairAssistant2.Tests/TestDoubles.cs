using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

internal sealed class FakeRegistry : IRegistryReader
{
    public object? Value { get; set; }
    public bool MiniNt { get; set; }
    public object? GetValue(string hiveRelativeKeyPath, string valueName) => Value;
    public bool KeyExists(string hiveRelativeKeyPath) => MiniNt;
}
internal sealed class FakeApi : IFirmwareApi
{
    public uint? Value { get; set; }
    public uint? GetFirmwareType() => Value;
}
internal sealed class FakeEnvironment : IEnvironmentInfo
{
    public bool IsWindows => false;
    public string SystemRoot { get; set; } = "";
    public string? GetEnvVar(string name) => null;
}
internal sealed class FakeVolumes : IVolumeProvider
{
    public IReadOnlyList<VolumeInfo> Volumes { get; set; } = Array.Empty<VolumeInfo>();
    public IReadOnlyList<VolumeInfo> GetVolumes() => Volumes;
}
internal sealed class FakeFileSystem : IFileSystem
{
    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> Lengths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string ExecutableDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "bra-exe");
    public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "bra-temp");
    private static string Normalize(string path) => path.Replace('/', '\\');
    public bool FileExists(string path) => Files.Any(x => string.Equals(Normalize(x), Normalize(path), StringComparison.OrdinalIgnoreCase));
    public bool DirectoryExists(string path) => Directories.Any(x => string.Equals(Normalize(x), Normalize(path), StringComparison.OrdinalIgnoreCase));
    public IEnumerable<string> EnumerateFiles(string dir, string pattern) => Files.Where(x => Path.GetDirectoryName(x)?.Equals(dir, StringComparison.OrdinalIgnoreCase) == true && Path.GetFileName(x).StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase));
    public void CreateDirectory(string path) { Directories.Add(Normalize(path)); if (!path.Contains(@":\", StringComparison.Ordinal)) Directory.CreateDirectory(path); }
    public void CopyFile(string src, string dst, bool overwrite) { Files.Add(Normalize(dst)); }
    public long GetFileLength(string path) => Lengths.FirstOrDefault(x => string.Equals(Normalize(x.Key), Normalize(path), StringComparison.OrdinalIgnoreCase)).Value;
}
internal sealed class NullApi : IFirmwareApi { public uint? GetFirmwareType() => null; }
internal sealed class NullLog : ILogSink { public void Log(string message) { } }
