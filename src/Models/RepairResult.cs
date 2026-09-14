namespace WindowsBootRepair.Models
{
    public class RepairResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? CommandExitCode { get; set; }
        public string CommandOutput { get; set; } = "";
        public string CommandError { get; set; } = "";
        public bool PostRepairVerificationPassed { get; set; }
        public List<string> VerificationResults { get; set; } = new();
        public DateTime ExecutedAt { get; set; }
    }
}
