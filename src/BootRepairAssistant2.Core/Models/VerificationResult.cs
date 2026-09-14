namespace BootRepairAssistant2.Core;

public record VerificationResult(
    RepairOutcome Outcome,
    IReadOnlyList<(string Check, bool Passed, string Detail)> Checks);
