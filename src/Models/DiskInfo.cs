namespace WindowsBootRepair.Models
{
    public class DiskInfo
    {
        public int DiskNumber { get; set; }
        public string Model { get; set; } = "Unknown";
        public long SizeBytes { get; set; }
        public bool IsRemovable { get; set; }
        public string InterfaceType { get; set; } = "Unknown";
        public List<PartitionInfo> Partitions { get; set; } = new();
        public DateTime DetectedAt { get; set; }
    }
}
