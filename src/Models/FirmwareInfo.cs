namespace WindowsBootRepair.Models
{
    public class FirmwareInfo
    {
        public string Mode { get; set; } = "Unknown";
        public string Confidence { get; set; } = "None";
        public int? RegistryValue { get; set; }
        public string? Error { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
