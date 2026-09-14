namespace WindowsBootRepair.Models
{
    public class WindowsInstallation
    {
        public string Path { get; set; } = "";
        public string WindowsPath { get; set; } = "";
        public string? Version { get; set; }
        public bool HasWinloadEFI { get; set; }
        public bool SystemConfigExists { get; set; }
        public char DriveLetter { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
