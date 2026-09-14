namespace BootRepairAssistant2.Core;

public sealed class WinPeDetector
{
    private readonly IRegistryReader registry;
    private readonly IEnvironmentInfo environment;

    public WinPeDetector(
        IRegistryReader registry,
        IEnvironmentInfo environment)
    {
        this.registry = registry;
        this.environment = environment;
    }

    public (bool IsWinPe, string Details) Detect()
    {
        var miniNt = registry.KeyExists(
            @"SYSTEM\CurrentControlSet\Control\MiniNT");
        var ramDisk = environment.SystemRoot.StartsWith(
            @"X:\",
            StringComparison.OrdinalIgnoreCase);

        var details = miniNt && ramDisk
            ? @"MiniNT registry key and SystemRoot X:\ evidence"
            : miniNt
                ? "MiniNT registry key detected"
                : ramDisk
                    ? @"SystemRoot starts with X:\ (WinPE ramdisk)"
                    : "No WinPE evidence detected";

        return (miniNt || ramDisk, details);
    }
}
