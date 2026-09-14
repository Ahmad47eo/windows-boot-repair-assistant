namespace WindowsBootRepair.Models
{
    public class DiagnosticReport
    {
        public DateTime GeneratedAt { get; set; }
        public string Version { get; set; } = "1.0.0";
        public FirmwareInfo Firmware { get; set; } = new();
        public List<DiskInfo> Disks { get; set; } = new();
        public List<WindowsInstallation> WindowsInstallations { get; set; } = new();
        public EFIPartition? EFIPartition { get; set; }
        public BootDiagnosticResult BootDiagnostics { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string RecommendedAction { get; set; } = "";
    }
}
