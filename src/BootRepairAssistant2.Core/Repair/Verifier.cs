using System.Text.RegularExpressions;

namespace BootRepairAssistant2.Core;

public sealed class Verifier
{
    private readonly IFileSystem fileSystem;

    public Verifier(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public VerificationResult Verify(string efiLetter)
    {
        if (!Regex.IsMatch(efiLetter, "^[A-Za-z]$"))
        {
            throw new ArgumentException(
                "EFI drive letter must be a single character.",
                nameof(efiLetter));
        }

        var root = efiLetter.ToUpperInvariant() +
            @":\EFI\Microsoft\Boot";
        var checks = new List<(string Check, bool Passed, string Detail)>();

        var bootmgfw = Required("bootmgfw.efi", root, checks);
        var bcd = Required("BCD", root, checks);
        var bootmgr = fileSystem.FileExists(
            Path.Combine(root, "bootmgr.efi"));
        checks.Add((
            "bootmgr.efi",
            bootmgr,
            bootmgr ? "present" : "missing (optional)"));

        var fallback = fileSystem.FileExists(
            efiLetter.ToUpperInvariant() +
            @":\EFI\Boot\bootx64.efi");
        checks.Add((
            "bootx64.efi",
            fallback,
            fallback ? "present" : "missing (optional)"));

        var nonEmpty = bcd
            && fileSystem.GetFileLength(Path.Combine(root, "BCD")) > 0;
        checks.Add((
            "BCD non-empty",
            nonEmpty,
            nonEmpty ? "size is greater than zero" : "empty or missing"));

        var requiredPassed = bootmgfw && bcd && nonEmpty;
        var optionalPassed = bootmgr && fallback;
        var outcome = !requiredPassed
            ? RepairOutcome.Failed
            : optionalPassed
                ? RepairOutcome.Success
                : RepairOutcome.Warning;
        return new VerificationResult(outcome, checks);
    }

    private bool Required(
        string name,
        string root,
        List<(string Check, bool Passed, string Detail)> checks)
    {
        var present = fileSystem.FileExists(Path.Combine(root, name));
        checks.Add((
            name,
            present,
            present ? "present" : "missing (required)"));
        return present;
    }
}
