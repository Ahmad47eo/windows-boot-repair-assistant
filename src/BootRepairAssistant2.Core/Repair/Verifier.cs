using System.Text.RegularExpressions;

namespace BootRepairAssistant2.Core;

public sealed class Verifier
{
    private readonly IFileSystem fileSystem;

    public Verifier(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public VerificationResult Verify(string rootPath)
    {
        var root = NormalizeRoot(rootPath);
        if (root is null)
        {
            throw new ArgumentException(
                "EFI root must be a drive root or volume GUID path.",
                nameof(rootPath));
        }

        var bootRoot = CombineRoot(root, @"EFI\Microsoft\Boot");
        var checks = new List<(string Check, bool Passed, string Detail)>();

        var bootmgfw = Required("bootmgfw.efi", bootRoot, checks);
        var bcd = Required("BCD", bootRoot, checks);
        var bootmgr = fileSystem.FileExists(
            CombineRoot(bootRoot, "bootmgr.efi"));
        checks.Add((
            "bootmgr.efi",
            bootmgr,
            bootmgr ? "present" : "missing (optional)"));

        var fallback = fileSystem.FileExists(
            CombineRoot(root, @"EFI\Boot\bootx64.efi"));
        checks.Add((
            "bootx64.efi",
            fallback,
            fallback ? "present" : "missing (optional)"));

        var nonEmpty = bcd
            && fileSystem.GetFileLength(CombineRoot(bootRoot, "BCD")) > 0;
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

    private static string? NormalizeRoot(string rootPath)
    {
        if (Regex.IsMatch(rootPath, "^[A-Za-z]$"))
        {
            return rootPath.ToUpperInvariant() + @":\";
        }

        if (rootPath.StartsWith(
                @"\\?\Volume{",
                StringComparison.OrdinalIgnoreCase)
            && !rootPath.EndsWith('\\'))
        {
            return rootPath + '\\';
        }

        if (rootPath.EndsWith('\\'))
        {
            return rootPath;
        }

        return rootPath.Contains(':')
            ? rootPath + '\\'
            : null;
    }

    private bool Required(
        string name,
        string root,
        List<(string Check, bool Passed, string Detail)> checks)
    {
        var present = fileSystem.FileExists(CombineRoot(root, name));
        checks.Add((
            name,
            present,
            present ? "present" : "missing (required)"));
        return present;
    }

    private static string CombineRoot(string root, string relativePath)
    {
        return root.TrimEnd('\\', '/')
            + "\\"
            + relativePath.TrimStart('\\', '/');
    }
}
