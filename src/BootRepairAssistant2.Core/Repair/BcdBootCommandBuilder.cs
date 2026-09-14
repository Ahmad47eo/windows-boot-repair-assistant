using System.Text.RegularExpressions;

namespace BootRepairAssistant2.Core;

public static class BcdBootCommandBuilder
{
    private static readonly Regex DriveLetter =
        new("^[A-Za-z]$", RegexOptions.Compiled);

    public static (string FileName, IReadOnlyList<string> Arguments) Build(
        string windowsPath,
        string efiLetter)
    {
        if (!DriveLetter.IsMatch(efiLetter))
        {
            throw new ArgumentException(
                "EFI drive letter must be a single A-Z character.",
                nameof(efiLetter));
        }

        if (windowsPath.Contains('"')
            || windowsPath.Contains('&')
            || windowsPath.Contains('|')
            || windowsPath.Contains(';'))
        {
            throw new ArgumentException(
                "Windows path contains unsafe command characters.",
                nameof(windowsPath));
        }

        return (
            "bcdboot",
            new[]
            {
                windowsPath,
                "/s",
                efiLetter.TrimEnd(':') + ":",
                "/f",
                "UEFI"
            });
    }
}
