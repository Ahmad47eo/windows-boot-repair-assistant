namespace WindowsBootRepair.Models
{
    public class PartitionInfo
    {
        public int DiskNumber { get; set; }
        public int PartitionNumber { get; set; }
        public long SizeBytes { get; set; }
        public string PartitionType { get; set; } = "Unknown";
        public DateTime DetectedAt { get; set; }
    }
}
