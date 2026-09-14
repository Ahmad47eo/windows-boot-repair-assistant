namespace BootRepairAssistant2.Core;

public sealed class EfiPartitionFinder
{
    public static readonly Guid EspType =
        Guid.Parse("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");

    private readonly IVolumeProvider volumes;
    private readonly IFileSystem fileSystem;
    private readonly ILogSink log;
    private readonly string? windowsDrive;

    public EfiPartitionFinder(
        IVolumeProvider volumes,
        IFileSystem fileSystem,
        ILogSink? log = null,
        string? windowsDrive = null)
    {
        this.volumes = volumes;
        this.fileSystem = fileSystem;
        this.log = log ?? NullLogSink.Instance;
        this.windowsDrive = windowsDrive?
            .TrimEnd(':')
            .ToUpperInvariant();
    }

    public IReadOnlyList<EfiPartitionCandidate> Find(
        string? windowsDriveOverride = null)
    {
        var candidates = new List<EfiPartitionCandidate>();
        var excludedWindowsDrive = windowsDriveOverride?
            .TrimEnd(':')
            .ToUpperInvariant() ?? windowsDrive;

        foreach (var volume in volumes.GetVolumes())
        {
            var score = 0;
            var reasons = new List<string>();
            var drive = volume.DriveLetter?
                .TrimEnd(':')
                .ToUpperInvariant();

            if (volume.GptPartitionType == EspType)
            {
                score += 50;
                reasons.Add("GPT EFI System Partition type");
            }

            if (string.Equals(
                    volume.FileSystem,
                    "FAT32",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    volume.FileSystem,
                    "FAT",
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
                reasons.Add("FAT filesystem");
            }

            if (volume.SizeBytes is >= 32L * 1024 * 1024
                and <= 2L * 1024 * 1024 * 1024)
            {
                score += 10;
                reasons.Add("size is typical for ESP");
            }

            var root = drive is null
                ? volume.VolumeGuidPath
                : drive + @":\";
            if (fileSystem.DirectoryExists(
                    Path.Combine(root, @"EFI\Microsoft\Boot")))
            {
                score += 15;
                reasons.Add("Microsoft EFI boot directory");
            }

            if (fileSystem.FileExists(
                    Path.Combine(root, @"EFI\Boot\bootx64.efi")))
            {
                score += 5;
                reasons.Add("fallback bootx64.efi");
            }

            if (string.Equals(
                    volume.FileSystem,
                    "NTFS",
                    StringComparison.OrdinalIgnoreCase))
            {
                score -= 100;
                reasons.Add("NTFS is not an EFI filesystem");
            }

            if (excludedWindowsDrive is not null
                && drive == excludedWindowsDrive)
            {
                score -= 100;
                reasons.Add("Windows installation volume");
            }

            if (volume.SizeBytes > 4L * 1024 * 1024 * 1024)
            {
                score -= 50;
                reasons.Add("volume is larger than 4 GB");
            }

            if (score >= 30)
            {
                candidates.Add(new EfiPartitionCandidate(
                    volume,
                    score,
                    reasons));
            }
        }

        log.Log($"EFI candidates found: {candidates.Count}");
        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
    }
}
