namespace BootRepairAssistant2.Core;

public record VolumeInfo(
    string? DriveLetter,
    string VolumeGuidPath,
    string? FileSystem,
    long SizeBytes,
    Guid? GptPartitionType,
    bool IsGpt,
    string? Label);
