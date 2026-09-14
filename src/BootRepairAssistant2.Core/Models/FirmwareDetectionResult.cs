namespace BootRepairAssistant2.Core;

public record FirmwareDetectionResult(
    FirmwareMode Mode,
    string Source,
    string Details);
