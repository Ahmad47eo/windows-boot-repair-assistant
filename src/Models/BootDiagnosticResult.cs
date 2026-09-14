namespace WindowsBootRepair.Models
{
    public class BootDiagnosticResult
    {
        public DateTime CheckedAt { get; set; }
        public int WindowsInstallationsFound { get; set; }
        public bool EFIPartitionFound { get; set; }
        public string BootFilesStatus { get; set; } = "Unknown";
        public List<string> Warnings { get; set; } = new();
    }
}
