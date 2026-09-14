namespace BootRepairAssistant2.Core;

public static class StatusText
{
    public static string ForFirmware(FirmwareDetectionResult result)
    {
        return result.Mode switch
        {
            FirmwareMode.Uefi => "UEFI detected",
            FirmwareMode.LegacyBios => "Legacy BIOS detected",
            _ => "Firmware unknown"
        };
    }
}
