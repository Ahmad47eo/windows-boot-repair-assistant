namespace BootRepairAssistant2.Core;

public interface IRegistryReader
{
    object? GetValue(string hiveRelativeKeyPath, string valueName);

    bool KeyExists(string hiveRelativeKeyPath);
}
