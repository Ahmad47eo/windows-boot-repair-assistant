using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Repair
{
    public static class BackupManager
    {
        public static string? CreateBackup(EFIPartition efi)
        {
            try
            {
                BootRepairLogger.Log("Creating BCD backup...");

                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var backupDir = Path.Combine(appDir, "backups");
                Directory.CreateDirectory(backupDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFile = Path.Combine(backupDir, $"BCD_backup_{timestamp}.bak");

                var bcdPath = Path.Combine(efi.DriveLetter.ToString(), @"EFI\Microsoft\Boot\BCD");

                if (!File.Exists(bcdPath))
                {
                    BootRepairLogger.Log($"BCD file not found at {bcdPath}");
                    return null;
                }

                File.Copy(bcdPath, backupFile, true);
                BootRepairLogger.Log($"Backup created: {backupFile}");
                return backupFile;
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Backup error: {ex.Message}");
                return null;
            }
        }
    }
}
