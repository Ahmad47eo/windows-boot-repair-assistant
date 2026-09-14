using System.Windows;
using WindowsBootRepair.Models;

namespace WindowsBootRepair.UI
{
    public class ReportGenerator
    {
        public DiagnosticReport GenerateReport(
            FirmwareInfo firmware,
            List<DiskInfo> disks,
            List<WindowsInstallation> windows,
            EFIPartition? efi,
            BootDiagnosticResult bootDiags)
        {
            var report = new DiagnosticReport
            {
                GeneratedAt = DateTime.Now,
                Version = "1.0.0",
                Firmware = firmware,
                Disks = disks,
                WindowsInstallations = windows,
                EFIPartition = efi,
                BootDiagnostics = bootDiags
            };

            // Add warnings
            report.Warnings.AddRange(bootDiags.Warnings);

            if (firmware.Mode != "UEFI")
            {
                report.Warnings.Add("System is not in UEFI mode. Repair features disabled.");
            }

            // Generate recommendation
            if (firmware.Mode == "UEFI" && windows.Count > 0 && efi != null && bootDiags.BootFilesStatus == "Degraded")
            {
                report.RecommendedAction = "Select 🛠️ REPAIR UEFI BOOT to rebuild boot configuration.";
            }
            else if (firmware.Mode != "UEFI")
            {
                report.RecommendedAction = "Boot in UEFI mode to enable repair features.";
            }
            else if (windows.Count == 0)
            {
                report.RecommendedAction = "No Windows installation detected. Cannot repair.";
            }
            else if (efi == null)
            {
                report.RecommendedAction = "EFI System Partition not found. Cannot repair.";
            }
            else
            {
                report.RecommendedAction = "System appears to be in good condition.";
            }

            return report;
        }

        public string FormatReportForDisplay(DiagnosticReport report)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("        WINDOWS BOOT REPAIR ASSISTANT - DIAGNOSTIC REPORT");
            sb.AppendLine("═══════════════════════════════════════════════════════════════\n");

            sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Application Version: {report.Version}\n");

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("FIRMWARE DETECTION");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            var fwIcon = report.Firmware.Mode switch
            {
                "UEFI" => "🟢",
                "Legacy BIOS" => "🟡",
                _ => "⚪"
            };
            sb.AppendLine($"Status: {fwIcon} {report.Firmware.Mode}");
            sb.AppendLine($"Confidence: {report.Firmware.Confidence}");
            if (report.Firmware.RegistryValue.HasValue)
            {
                sb.AppendLine($"Registry Value: {report.Firmware.RegistryValue}");
            }
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("PHYSICAL DISKS");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var disk in report.Disks)
            {
                var typeLabel = disk.IsRemovable ? "[Removable]" : "[Internal]";
                sb.AppendLine($"Disk {disk.DiskNumber}: {FormatBytes(disk.SizeBytes)} {typeLabel}");
            }
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("WINDOWS INSTALLATIONS FOUND");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            if (report.WindowsInstallations.Count == 0)
            {
                sb.AppendLine("No Windows installations detected.");
            }
            else
            {
                for (int i = 0; i < report.WindowsInstallations.Count; i++)
                {
                    var win = report.WindowsInstallations[i];
                    sb.AppendLine($"{i + 1}. {win.Path}");
                    sb.AppendLine($"   Version: {win.Version ?? "Unknown"}");
                    sb.AppendLine($"   System Config: {(win.SystemConfigExists ? "✓" : "✗")}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("EFI SYSTEM PARTITION");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            if (report.EFIPartition == null)
            {
                sb.AppendLine("🔴 Not found");
            }
            else
            {
                sb.AppendLine("🟢 Detected");
                sb.AppendLine($"Drive Letter: {report.EFIPartition.DriveLetter}:");
                sb.AppendLine($"Filesystem: {report.EFIPartition.Filesystem}");
                sb.AppendLine($"Size: {FormatBytes(report.EFIPartition.SizeBytes)}");
                sb.AppendLine($"Accessible: {(report.EFIPartition.IsAccessible ? "✓" : "✗")}");
            }
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("BOOT CONFIGURATION");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            var bootIcon = report.BootDiagnostics.BootFilesStatus switch
            {
                "Healthy" => "🟢",
                "Degraded" => "🟡",
                "Missing" => "🔴",
                _ => "⚪"
            };
            sb.AppendLine($"Boot Files Status: {bootIcon} {report.BootDiagnostics.BootFilesStatus}");
            sb.AppendLine();

            if (report.Warnings.Count > 0)
            {
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                sb.AppendLine("WARNINGS & ISSUES");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (var warning in report.Warnings)
                {
                    sb.AppendLine($"⚠️ {warning}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("RECOMMENDED ACTION");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"→ {report.RecommendedAction}");
            sb.AppendLine();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        public string FormatRepairResult(Models.RepairResult result)
        {
            var sb = new System.Text.StringBuilder();

            if (result.Success)
            {
                sb.AppendLine("\n✓✓✓ REPAIR COMPLETED SUCCESSFULLY ✓✓✓\n");
                sb.AppendLine("Boot files have been repaired and copied to the EFI partition.");
                sb.AppendLine("\nVerification Results:");
                foreach (var verification in result.VerificationResults)
                {
                    sb.AppendLine($"  {verification}");
                }
                sb.AppendLine("\nRestart your computer manually when ready.");
            }
            else
            {
                sb.AppendLine("\n✗✗✗ REPAIR FAILED ✗✗✗\n");
                sb.AppendLine($"Error: {result.ErrorMessage}");
                if (result.CommandExitCode.HasValue)
                {
                    sb.AppendLine($"Exit Code: {result.CommandExitCode}");
                }
            }

            return sb.ToString();
        }

        private string FormatBytes(long bytes)
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
