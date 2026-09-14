using WindowsBootRepair.Commands;
using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Repair
{
    public static class RepairEngine
    {
        public static RepairResult ExecuteRepair(WindowsInstallation windows, EFIPartition efi)
        {
            BootRepairLogger.Log("=== REPAIR ENGINE STARTED ===");

            var result = new RepairResult { ExecutedAt = DateTime.Now };

            try
            {
                // Verify UEFI mode one more time
                BootRepairLogger.Log("Re-verifying UEFI mode...");
                if (!FirmwareDetector.VerifyUEFIMode())
                {
                    result.Success = false;
                    result.ErrorMessage = "System is not in UEFI mode. Repair cancelled.";
                    BootRepairLogger.Log("UEFI verification failed. Repair cancelled.");
                    return result;
                }

                // Create backup
                BootRepairLogger.Log("Creating BCD backup...");
                var backup = BackupManager.CreateBackup(efi);
                if (backup == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to create backup. Repair cancelled.";
                    BootRepairLogger.Log("Backup failed. Repair cancelled.");
                    return result;
                }

                // Execute bcdboot
                BootRepairLogger.Log($"Executing bcdboot for {windows.Path}");
                var bcdbootResult = BCDBootExecutor.ExecuteBCDBoot(windows, efi);

                if (!bcdbootResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = bcdbootResult.ErrorMessage;
                    result.CommandError = bcdbootResult.CommandError;
                    result.CommandExitCode = bcdbootResult.CommandExitCode;
                    BootRepairLogger.Log($"BCDBOOT failed with exit code {bcdbootResult.CommandExitCode}");
                    return result;
                }

                // Verify repair
                BootRepairLogger.Log("Verifying repair...");
                var verification = PostRepairVerifier.VerifyRepair(efi);

                result.Success = verification.Success;
                result.PostRepairVerificationPassed = verification.Success;
                result.VerificationResults = verification.VerificationResults;
                result.CommandOutput = bcdbootResult.CommandOutput;
                result.CommandExitCode = 0;

                BootRepairLogger.Log($"Repair completed. Success: {result.Success}");
                return result;
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Repair engine error: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}
