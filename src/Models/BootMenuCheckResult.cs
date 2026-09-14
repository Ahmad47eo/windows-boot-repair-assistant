namespace WindowsBootRepair.Models
{
    public class BootMenuCheckResult
    {
        public DateTime CheckedAt { get; set; }
        public bool WindowsBootManagerFound { get; set; }
        public string Status { get; set; } = "Unknown";
        public string Message { get; set; } = "";
    }
}
