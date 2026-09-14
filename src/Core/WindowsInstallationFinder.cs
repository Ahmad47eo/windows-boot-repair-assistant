using WindowsBootRepair.Logging;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.Core
{
    public static class WindowsInstallationFinder
    {
        public static List<WindowsInstallation> FindInstallations(List<DiskInfo> disks)
        {
            BootRepairLogger.Log("Finding Windows installations...");
            var installations = new List<WindowsInstallation>();

            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();

                foreach (var drive in drives)
                {
                    try
                    {
                        var windowsPath = Path.Combine(drive.Name, "Windows");
                        var systemConfigPath = Path.Combine(windowsPath, "System32", "config", "SYSTEM");

                        if (Directory.Exists(windowsPath) && File.Exists(systemConfigPath))
                        {
                            var installation = new WindowsInstallation
                            {
                                Path = drive.Name.TrimEnd('\\'),
                                WindowsPath = windowsPath,
                                SystemConfigExists = true,
                                DetectedAt = DateTime.Now,
                                DriveLetter = drive.Name[0]
                            };

                            installations.Add(installation);
                            BootRepairLogger.Log($"Windows found: {installation.Path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        BootRepairLogger.Log($"Drive check error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                BootRepairLogger.Log($"Search error: {ex.Message}");
            }

            return installations;
        }
    }
}
