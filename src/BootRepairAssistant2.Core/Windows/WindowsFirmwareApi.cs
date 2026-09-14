using System.Runtime.InteropServices;

namespace BootRepairAssistant2.Core;

public sealed class WindowsFirmwareApi : IFirmwareApi
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFirmwareType(out uint firmwareType);

    public uint? GetFirmwareType()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return GetFirmwareType(out var value) ? value : null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }
}
