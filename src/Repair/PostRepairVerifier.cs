using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Repair
{
    public static class PostRepairVerifier
    {
        public static RepairResult VerifyRepair(EFIPartition efi)
        {
            var result = new RepairResult { ExecutedAt = DateTime.Now, VerificationResults = new() };

            try
            {
                BootRepairLogger.Log("Starting post-repair verification...");

                var bootDir = Path.Combine(efi.DriveLetter.ToString(), @"EFI\Microsoft\Boot");
                var bootmgrwEFI = Path.Combine(bootDir, "bootmgfw.efi");
                var bcdFile = Path.Combine(bootDir, "BCD");

                if (File.Exists(bootmgrwEFI))
                {
                    result.VerificationResults.Add("✓ bootmgfw.efi exists");
                    BootRepairLogger.Log("✓ bootmgfw.efi verified");
                }
                else
                {
                    result.VerificationResults.Add("✗ bootmgfw.efi missing");
                    BootRepairLogger.Log("✗ bootmgfw.efi not found");
                }

                if (File.Exists(bcdFile))
                {
                    result.VerificationResults.Add("✓ BCD file exists");
                    BootRepairLogger.Log("✓ BCD verified");
                }
                else
                {
                    result.VerificationResults.Add("✗ BCD file missing");
                    BootRepairLogger.Log("✗ BCD not found");
                }

                result.Success = File.Exists(bootmgrwEFI) && File.Exists(bcdFile);
                result.PostRepairVerificationPassed = result.Success;

                BootRepairLogger.Log($"Post-repair verification: {(result.Success ? "PASSED" : "FAILED")}");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                BootRepairLogger.Log($"Verification error: {ex.Message}");
                return result;
            }
        }
    }
}
