namespace BootRepairAssistant2.Core;

public record EfiPartitionCandidate(
    VolumeInfo Volume,
    int Score,
    IReadOnlyList<string> Reasons);
