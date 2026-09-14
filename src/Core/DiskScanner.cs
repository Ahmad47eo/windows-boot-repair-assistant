using System.Management;
using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Core
{
    public static class DiskScanner
    {
        public static List<DiskInfo> ScanDisks()
        {
            BootRepairLogger.Log("Scanning disks...");
            var disks = new List<DiskInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (var disk in searcher.Get())
                    {
                        try
                        {
                            int diskNumber = int.Parse(disk["Index"].ToString() ?? "0");
                            string model = disk["Model"]?.ToString() ?? "Unknown";
                            long size = long.Parse(disk["Size"]?.ToString() ?? "0");
                            bool removable = bool.Parse(disk["Removable"]?.ToString() ?? "false");

                            var diskInfo = new DiskInfo
                            {
                                DiskNumber = diskNumber,
                                Model = model,
                                SizeBytes = size,
                                IsRemovable = removable,
                                DetectedAt = DateTime.Now
                            };

                            disks.Add(diskInfo);
                            BootRepairLogger.Log($"Disk {diskNumber}: {model} ({FormatBytes(size)})");
                        }
                        catch (Exception ex)
                        {
                            BootRepairLogger.Log($"Error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Disk scan error: {ex.Message}");
            }

            return disks;
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
