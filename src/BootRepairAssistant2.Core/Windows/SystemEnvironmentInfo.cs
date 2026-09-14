namespace BootRepairAssistant2.Core;

public sealed class SystemEnvironmentInfo : IEnvironmentInfo
{
    public bool IsWindows => OperatingSystem.IsWindows();

    public string SystemRoot => Environment.GetEnvironmentVariable("SystemRoot") ?? string.Empty;

    public string? GetEnvVar(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }
}
