using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Core
{
    public static class EFIPartitionFinder
    {
        public static EFIPartition? FindEFIPartition(List<DiskInfo> disks)
        {
            BootRepairLogger.Log("Finding EFI partition...");

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;

                    try
                    {
                        if (drive.DriveFormat != "FAT32") continue;

                        var efiBootPath = Path.Combine(drive.Name, "EFI", "Microsoft", "Boot");
                        if (!Directory.Exists(efiBootPath)) continue;

                        var efiPartition = new EFIPartition
                        {
                            DriveLetter = drive.Name[0],
                            Filesystem = "FAT32",
                            SizeBytes = drive.TotalSize,
                            DetectedAt = DateTime.Now,
                            IsAccessible = true
                        };

                        BootRepairLogger.Log($"EFI partition found: {drive.Name}");
                        return efiPartition;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"EFI search error: {ex.Message}");
            }

            return null;
        }
    }
}
