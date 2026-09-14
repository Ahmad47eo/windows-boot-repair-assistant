using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

internal sealed class FakeRegistry : IRegistryReader
{
    public object? Value { get; set; }

    public bool MiniNt { get; set; }

    public object? GetValue(string hiveRelativeKeyPath, string valueName)
    {
        return Value;
    }

    public bool KeyExists(string hiveRelativeKeyPath)
    {
        return MiniNt;
    }
}

internal sealed class FakeApi : IFirmwareApi
{
    public uint? Value { get; set; }

    public uint? GetFirmwareType()
    {
        return Value;
    }
}

internal sealed class FakeEnvironment : IEnvironmentInfo
{
    public bool IsWindows => false;

    public string SystemRoot { get; set; } = string.Empty;

    public string? GetEnvVar(string name)
    {
        return null;
    }
}

internal sealed class FakeVolumes : IVolumeProvider
{
    public IReadOnlyList<VolumeInfo> Volumes { get; set; } =
        Array.Empty<VolumeInfo>();

    public IReadOnlyList<VolumeInfo> GetVolumes()
    {
        return Volumes;
    }
}

internal sealed class FakeFileSystem : IFileSystem
{
    public HashSet<string> Files { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Directories { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, long> Lengths { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path)
    {
        return Files.Any(file => PathsEqual(file, path));
    }

    public bool DirectoryExists(string path)
    {
        return Directories.Any(directory => PathsEqual(directory, path));
    }

    public IEnumerable<string> EnumerateFiles(string dir, string pattern)
    {
        return Files.Where(file =>
            string.Equals(
                Normalize(Path.GetDirectoryName(file) ?? string.Empty),
                Normalize(dir),
                StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(file).StartsWith(
                pattern.TrimEnd('*'),
                StringComparison.OrdinalIgnoreCase));
    }

    public void CreateDirectory(string path)
    {
        Directories.Add(path);
        if (!HasDrivePrefix(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public void CopyFile(string src, string dst, bool overwrite)
    {
        Files.Add(dst);
    }

    public long GetFileLength(string path)
    {
        var length = Lengths.FirstOrDefault(item =>
            PathsEqual(item.Key, path));
        return length.Equals(default(KeyValuePair<string, long>))
            ? 0
            : length.Value;
    }

    public void WriteAllText(string path, string text)
    {
        if (!HasDrivePrefix(path))
        {
            File.WriteAllText(path, text);
        }
    }

    public void DeleteFile(string path)
    {
        if (!HasDrivePrefix(path))
        {
            File.Delete(path);
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            Normalize(first),
            Normalize(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return path.Replace('/', '\\');
    }

    private static bool HasDrivePrefix(string path)
    {
        return path.Length >= 3
            && char.IsLetter(path[0])
            && path[1..3] == @":\";
    }
}

internal sealed class FakeRunner : IProcessRunner
{
    public int Calls { get; private set; }

    public CommandResult Result { get; set; } = new(
        "bcdboot",
        Array.Empty<string>(),
        0,
        string.Empty,
        string.Empty,
        false,
        false,
        TimeSpan.Zero);

    public Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeMounter : IVolumeMounter
{
    public int AssignCalls { get; private set; }

    public int RemoveCalls { get; private set; }

    public List<string> AssignedVolumes { get; } = new();

    public List<string> RemovedLetters { get; } = new();

    public string? AssignedLetter { get; set; } = "Z";

    public string? AssignLetter(string volumeGuidPath)
    {
        AssignCalls++;
        AssignedVolumes.Add(volumeGuidPath);
        return AssignedLetter;
    }

    public void RemoveLetter(string letter)
    {
        RemoveCalls++;
        RemovedLetters.Add(letter);
    }
}

internal sealed class NullApi : IFirmwareApi
{
    public uint? GetFirmwareType()
    {
        return null;
    }
}

internal sealed class NullLog : ILogSink
{
    public void Log(string message)
    {
    }
}
