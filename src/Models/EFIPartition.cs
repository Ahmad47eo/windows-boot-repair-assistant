namespace WindowsBootRepair.Models
{
    public class EFIPartition
    {
        public char DriveLetter { get; set; }
        public int DiskNumber { get; set; }
        public int PartitionNumber { get; set; }
        public string Filesystem { get; set; } = "FAT32";
        public long SizeBytes { get; set; }
        public string PartitionType { get; set; } = "";
        public bool IsAccessible { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
