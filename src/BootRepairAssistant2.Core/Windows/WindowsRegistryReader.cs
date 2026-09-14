using Microsoft.Win32;

namespace BootRepairAssistant2.Core;

public sealed class WindowsRegistryReader : IRegistryReader
{
    public object? GetValue(string hiveRelativeKeyPath, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(hiveRelativeKeyPath);
            return key?.GetValue(valueName);
        }
        catch
        {
            return null;
        }
    }

    public bool KeyExists(string hiveRelativeKeyPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(hiveRelativeKeyPath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
