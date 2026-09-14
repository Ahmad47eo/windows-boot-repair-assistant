namespace BootRepairAssistant2.Core;

public record RepairResult(
    RepairOutcome Outcome,
    string BackupDirectory,
    CommandResult? BcdBoot,
    VerificationResult? Verification,
    DiagnosticReport? PostScan,
    string Summary);
