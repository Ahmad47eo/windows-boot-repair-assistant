using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class BcdBootCommandBuilderTests
{
    [Fact] public void BuildsExactArgs() { var result = BcdBootCommandBuilder.Build(@"D:\Windows", "S"); Assert.Equal("bcdboot", result.FileName); Assert.Equal(new[] { @"D:\Windows", "/s", "S:", "/f", "UEFI" }, result.Arguments); }
    [Theory] [InlineData("")] [InlineData("SS")] [InlineData("1")] public void InvalidLetterThrows(string letter) => Assert.Throws<ArgumentException>(() => BcdBootCommandBuilder.Build(@"D:\Windows", letter));
    [Theory] [InlineData("\"")] [InlineData("&")] public void UnsafePathThrows(string token) => Assert.Throws<ArgumentException>(() => BcdBootCommandBuilder.Build(@"D:\Windows" + token, "S"));
}

public sealed class VerifierTests
{
    private static FakeFileSystem Files(bool optional = true, bool bcd = true) { var fs = new FakeFileSystem(); fs.Files.Add(@"S:\EFI\Microsoft\Boot\bootmgfw.efi"); if (bcd) { fs.Files.Add(@"S:\EFI\Microsoft\Boot\BCD"); fs.Lengths[@"S:\EFI\Microsoft\Boot\BCD"] = 5; } if (optional) { fs.Files.Add(@"S:\EFI\Microsoft\Boot\bootmgr.efi"); fs.Files.Add(@"S:\EFI\Boot\bootx64.efi"); } return fs; }
    [Fact] public void AllPresentIsSuccess() => Assert.Equal(RepairOutcome.Success, new Verifier(Files()).Verify("S").Outcome);
    [Fact] public void OptionalMissingIsWarning() => Assert.Equal(RepairOutcome.Warning, new Verifier(Files(false)).Verify("S").Outcome);
    [Fact] public void BcdMissingIsFailed() => Assert.Equal(RepairOutcome.Failed, new Verifier(Files(true, false)).Verify("S").Outcome);
}

public sealed class DiagnosticReportTests
{
    private static DiagnosticReport Report(FirmwareMode mode, params EfiPartitionCandidate[] efi) => new() { Firmware = new(mode, "test", ""), WindowsCandidates = new[] { new WindowsInstallation("D", @"D:\Windows", Array.Empty<string>(), Array.Empty<string>()) }, SelectedWindows = new WindowsInstallation("D", @"D:\Windows", Array.Empty<string>(), Array.Empty<string>()), EfiCandidates = efi, SelectedEfi = efi.FirstOrDefault() };
    private static EfiPartitionCandidate Efi(string drive, int score = 50) => new(new VolumeInfo(drive, $@"\\?\Volume{{{drive}}}\", "FAT32", 100 * 1024 * 1024, EfiPartitionFinder.EspType, true, ""), score, Array.Empty<string>());
    [Fact] public void LegacyIsBlocked() => Assert.False(Report(FirmwareMode.LegacyBios, Efi("S")).RepairAllowed);
    [Fact] public void AmbiguousIsBlocked() => Assert.False(Report(FirmwareMode.Uefi, Efi("S"), Efi("T")).RepairAllowed);
    [Fact] public void HappyIsAllowed() => Assert.True(Report(FirmwareMode.Uefi, Efi("S")).RepairAllowed);
}

public sealed class BackupManagerTests
{
    [Fact] public void UsesUniqueDirectoryNaming()
    {
        var fs = new FakeFileSystem { ExecutableDirectory = Path.Combine(Path.GetTempPath(), "bra-backup-" + Guid.NewGuid().ToString("N")) }; var report = new DiagnosticReport { Firmware = new(FirmwareMode.Uefi, "test", ""), SelectedWindows = new WindowsInstallation("D", @"D:\Windows", Array.Empty<string>(), Array.Empty<string>()), SelectedEfi = new EfiPartitionCandidate(new VolumeInfo("S", @"\\?\Volume{s}\", "FAT32", 100, EfiPartitionFinder.EspType, true, ""), 50, Array.Empty<string>()) };
        var first = new BackupManager(fs).CreateBackup(report); fs.Directories.Add(first); var second = new BackupManager(fs).CreateBackup(report); Assert.EndsWith("_2", second);
    }
}
