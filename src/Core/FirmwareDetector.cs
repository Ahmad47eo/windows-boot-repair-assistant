using Microsoft.Win32;
using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Core
{
    public static class FirmwareDetector
    {
        public static FirmwareInfo DetectFirmware()
        {
            BootRepairLogger.Log("Detecting firmware mode...");

            try
            {
                const string registryPath = @"SYSTEM\CurrentControlSet\Control";
                const string valueName = "PEFirmwareType";

                using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
                {
                    if (key == null)
                    {
                        BootRepairLogger.Log("Registry key not found. Firmware mode: Unknown");
                        return new FirmwareInfo { Mode = "Unknown", Confidence = "Low", DetectedAt = DateTime.Now };
                    }

                    var value = key.GetValue(valueName);
                    if (value == null)
                    {
                        BootRepairLogger.Log("PEFirmwareType not found");
                        return new FirmwareInfo { Mode = "Unknown", Confidence = "Low", DetectedAt = DateTime.Now };
                    }

                    int firmwareType = (int)value;
                    BootRepairLogger.Log($"PEFirmwareType: {firmwareType}");

                    var result = firmwareType switch
                    {
                        1 => new FirmwareInfo { Mode = "Legacy BIOS", Confidence = "Very High", RegistryValue = 1, DetectedAt = DateTime.Now },
                        2 => new FirmwareInfo { Mode = "UEFI", Confidence = "Very High", RegistryValue = 2, DetectedAt = DateTime.Now },
                        _ => new FirmwareInfo { Mode = "Unknown", Confidence = "Medium", RegistryValue = firmwareType, DetectedAt = DateTime.Now }
                    };

                    BootRepairLogger.Log($"Firmware: {result.Mode}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Firmware detection error: {ex.Message}");
                return new FirmwareInfo { Mode = "Unknown", Confidence = "None", Error = ex.Message, DetectedAt = DateTime.Now };
            }
        }

        public static bool VerifyUEFIMode()
        {
            var firmware = DetectFirmware();
            return firmware.Mode == "UEFI";
        }
    }
}
