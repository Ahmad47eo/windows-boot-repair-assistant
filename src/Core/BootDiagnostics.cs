using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Core
{
    public static class BootDiagnostics
    {
        public static BootDiagnosticResult CheckBootConfiguration(
            List<WindowsInstallation> installations,
            EFIPartition? efiPartition)
        {
            BootRepairLogger.Log("Checking boot config...");

            var result = new BootDiagnosticResult
            {
                CheckedAt = DateTime.Now,
                WindowsInstallationsFound = installations.Count,
                EFIPartitionFound = efiPartition != null,
                Warnings = new List<string>()
            };

            if (installations.Count == 0)
            {
                result.Warnings.Add("No Windows installations detected.");
            }

            if (efiPartition == null)
            {
                result.Warnings.Add("EFI System Partition not found.");
            }
            else
            {
                CheckEFIBootFiles(efiPartition, result);
            }

            return result;
        }

        private static void CheckEFIBootFiles(EFIPartition efi, BootDiagnosticResult result)
        {
            try
            {
                var bootDir = Path.Combine(efi.DriveLetter.ToString(), @"EFI\Microsoft\Boot");
                var bootmgrwEFI = Path.Combine(bootDir, "bootmgfw.efi");
                var bcdFile = Path.Combine(bootDir, "BCD");

                if (!File.Exists(bootmgrwEFI))
                {
                    result.Warnings.Add("EFI boot file missing.");
                }

                if (!File.Exists(bcdFile))
                {
                    result.Warnings.Add("BCD file missing.");
                }

                result.BootFilesStatus = (File.Exists(bootmgrwEFI) && File.Exists(bcdFile)) ? "Healthy" : "Degraded";
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Boot file check error: {ex.Message}");
            }
        }
    }
}
