namespace BootRepairAssistant2.Core;

public interface IEnvironmentInfo
{
    bool IsWindows { get; }

    string SystemRoot { get; }

    string? GetEnvVar(string name);
}
