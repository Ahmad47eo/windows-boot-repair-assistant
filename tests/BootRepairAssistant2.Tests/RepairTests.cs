using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class BcdBootCommandBuilderTests
{
    [Fact]
    public void BuildsExactArgs()
    {
        var result = BcdBootCommandBuilder.Build(
            @"D:\Windows",
            "S");

        Assert.Equal("bcdboot", result.FileName);
        Assert.Equal(
            new[] { @"D:\Windows", "/s", "S:", "/f", "UEFI" },
            result.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SS")]
    [InlineData("1")]
    public void InvalidLetterThrows(string letter)
    {
        Assert.Throws<ArgumentException>(() =>
            BcdBootCommandBuilder.Build(@"D:\Windows", letter));
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("&")]
    public void UnsafePathThrows(string token)
    {
        Assert.Throws<ArgumentException>(() =>
            BcdBootCommandBuilder.Build(
                @"D:\Windows" + token,
                "S"));
    }
}

public sealed class VerifierTests
{
    [Fact]
    public void AllPresentIsSuccess()
    {
        Assert.Equal(
            RepairOutcome.Success,
            new Verifier(Files()).Verify(@"S:\").Outcome);
    }

    [Fact]
    public void OptionalMissingIsWarning()
    {
        Assert.Equal(
            RepairOutcome.Warning,
            new Verifier(Files(false)).Verify(@"S:\").Outcome);
    }

    [Fact]
    public void BcdMissingIsFailed()
    {
        Assert.Equal(
            RepairOutcome.Failed,
            new Verifier(Files(true, false)).Verify(@"S:\").Outcome);
    }

    [Fact]
    public void GuidRootIsAccepted()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files.Add(
            @"\\?\Volume{esp}\EFI\Microsoft\Boot\bootmgfw.efi");
        fileSystem.Files.Add(
            @"\\?\Volume{esp}\EFI\Microsoft\Boot\BCD");
        fileSystem.Lengths[
            @"\\?\Volume{esp}\EFI\Microsoft\Boot\BCD"] = 5;
        fileSystem.Files.Add(
            @"\\?\Volume{esp}\EFI\Microsoft\Boot\bootmgr.efi");
        fileSystem.Files.Add(
            @"\\?\Volume{esp}\EFI\Boot\bootx64.efi");

        var result = new Verifier(fileSystem).Verify(
            @"\\?\Volume{esp}\");

        Assert.Equal(RepairOutcome.Success, result.Outcome);
    }

    private static FakeFileSystem Files(
        bool optional = true,
        bool bcd = true)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files.Add(
            @"S:\EFI\Microsoft\Boot\bootmgfw.efi");
        if (bcd)
        {
            fileSystem.Files.Add(@"S:\EFI\Microsoft\Boot\BCD");
            fileSystem.Lengths[@"S:\EFI\Microsoft\Boot\BCD"] = 5;
        }

        if (optional)
        {
            fileSystem.Files.Add(
                @"S:\EFI\Microsoft\Boot\bootmgr.efi");
            fileSystem.Files.Add(@"S:\EFI\Boot\bootx64.efi");
        }

        return fileSystem;
    }
}

public sealed class DiagnosticReportTests
{
    [Fact]
    public void LegacyIsBlocked()
    {
        Assert.False(
            Report(
                FirmwareMode.LegacyBios,
                Efi("S")).RepairAllowed);
    }

    [Fact]
    public void LegacyFirmwareIsError()
    {
        var report = Report(
            FirmwareMode.LegacyBios,
            Efi("S"));

        Assert.Equal(StatusLevel.Error, report.FirmwareStatus);
    }

    [Fact]
    public void AmbiguousIsBlocked()
    {
        Assert.False(
            Report(
                FirmwareMode.Uefi,
                Efi("S"),
                Efi("T")).RepairAllowed);
    }

    [Fact]
    public void StrongLeaderWithQualifiedRunnerUpIsAllowed()
    {
        var leader = Efi("S") with { Score = 100 };
        var runnerUp = Efi("T") with { Score = 35 };

        var report = Report(
            FirmwareMode.Uefi,
            leader,
            runnerUp);

        Assert.True(report.RepairAllowed);
        Assert.Equal(StatusLevel.Ok, report.EfiStatus);
    }

    [Fact]
    public void EqualTopScoresAreBlockedWithAmbiguousReason()
    {
        var report = Report(
            FirmwareMode.Uefi,
            Efi("S") with { Score = 100 },
            Efi("T") with { Score = 100 });

        Assert.False(report.RepairAllowed);
        Assert.Contains(
            "ambiguous",
            report.RepairBlockReason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InsufficientScoreMarginIsBlocked()
    {
        var report = Report(
            FirmwareMode.Uefi,
            Efi("S") with { Score = 50 },
            Efi("T") with { Score = 40 });

        Assert.False(report.RepairAllowed);
        Assert.Equal(StatusLevel.Warning, report.EfiStatus);
    }

    [Fact]
    public void HappyIsAllowed()
    {
        Assert.True(
            Report(
                FirmwareMode.Uefi,
                Efi("S")).RepairAllowed);
    }

    private static DiagnosticReport Report(
        FirmwareMode mode,
        params EfiPartitionCandidate[] efi)
    {
        var windows = new WindowsInstallation(
            "D",
            @"D:\Windows",
            Array.Empty<string>(),
            Array.Empty<string>());
        return new DiagnosticReport
        {
            Firmware = new(mode, "test", string.Empty),
            WindowsCandidates = new[] { windows },
            SelectedWindows = windows,
            EfiCandidates = efi,
            SelectedEfi = efi.FirstOrDefault()
        };
    }

    private static EfiPartitionCandidate Efi(string drive)
    {
        return new EfiPartitionCandidate(
            new VolumeInfo(
                drive,
                $@"\\?\Volume{{{drive}}}\",
                "FAT32",
                100 * 1024 * 1024,
                EfiPartitionFinder.EspType,
                true,
                string.Empty),
            50,
            Array.Empty<string>());
    }
}

public sealed class BackupManagerTests
{
    [Fact]
    public void UsesUniqueDirectoryNaming()
    {
        var fileSystem = new FakeFileSystem();
        var report = ValidReport("S");
        var manager = new BackupManager(fileSystem);

        var first = manager.CreateBackup(report);
        fileSystem.Directories.Add(first);
        var second = manager.CreateBackup(report);

        Assert.EndsWith("_2", second);
    }

    private static DiagnosticReport ValidReport(string? letter)
    {
        var windows = new WindowsInstallation(
            "D",
            @"D:\Windows",
            Array.Empty<string>(),
            Array.Empty<string>());
        var efi = new EfiPartitionCandidate(
            new VolumeInfo(
                letter,
                @"\\?\Volume{esp}\",
                "FAT32",
                100 * 1024 * 1024,
                EfiPartitionFinder.EspType,
                true,
                string.Empty),
            50,
            Array.Empty<string>());
        return new DiagnosticReport
        {
            Firmware = new(
                FirmwareMode.Uefi,
                "test",
                string.Empty),
            WindowsCandidates = new[] { windows },
            SelectedWindows = windows,
            EfiCandidates = new[] { efi },
            SelectedEfi = efi
        };
    }
}

public sealed class RepairEngineTests
{
    [Fact]
    public void DescribePlannedCommandWithLetter()
    {
        var engine = CreateEngine(
            new FakeRunner(),
            new FakeFileSystem());

        Assert.Equal(
            @"bcdboot D:\Windows /s S: /f UEFI",
            engine.DescribePlannedCommand(ValidReport("S")));
    }

    [Fact]
    public void DescribePlannedCommandWithoutLetter()
    {
        var engine = CreateEngine(
            new FakeRunner(),
            new FakeFileSystem());
        var command = engine.DescribePlannedCommand(ValidReport(null));

        Assert.Contains("no drive letter", command);
        Assert.Contains("<letter>:", command);
    }

    [Fact]
    public async Task TestModeDoesNotInvokeRunner()
    {
        var runner = new FakeRunner();
        var engine = CreateEngine(runner, VerificationFiles());

        var result = await engine.RepairAsync(
            ValidReport("S"),
            true,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.NotRun, result.Outcome);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task TestModeWithoutLetterDoesNotMountOrRun()
    {
        var runner = new FakeRunner();
        var mounter = new FakeMounter();
        var engine = CreateEngine(
            runner,
            VerificationFiles(),
            mounter);

        var result = await engine.RepairAsync(
            ValidReport(null),
            true,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.NotRun, result.Outcome);
        Assert.Equal(0, runner.Calls);
        Assert.Equal(0, mounter.AssignCalls);
        Assert.Contains("no drive letter", result.Summary);
        Assert.Contains("<letter>:", result.Summary);
    }

    [Fact]
    public async Task TestModeWithUnletteredWindowsDoesNotMountOrRun()
    {
        var runner = new FakeRunner();
        var mounter = new FakeMounter();
        var engine = CreateEngine(
            runner,
            VerificationFiles(),
            mounter);

        var result = await engine.RepairAsync(
            ValidReport("S", null),
            true,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.NotRun, result.Outcome);
        Assert.Equal(0, runner.Calls);
        Assert.Equal(0, mounter.AssignCalls);
        Assert.Contains("Windows volume", result.Summary);
        Assert.Contains("<letter>:\\Windows", result.Summary);
    }

    [Fact]
    public async Task RealModeSuccessWhenVerificationPasses()
    {
        var runner = new FakeRunner();
        var engine = CreateEngine(runner, VerificationFiles());

        var result = await engine.RepairAsync(
            ValidReport("S"),
            false,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.Success, result.Outcome);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task RealModeWarningWhenOptionalFileMissing()
    {
        var runner = new FakeRunner();
        var fileSystem = VerificationFiles();
        fileSystem.Files.Remove(
            @"S:\EFI\Microsoft\Boot\bootmgr.efi");
        var engine = CreateEngine(runner, fileSystem);

        var result = await engine.RepairAsync(
            ValidReport("S"),
            false,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.Warning, result.Outcome);
    }

    [Fact]
    public async Task RealModeAssignsAndRemovesUnletteredWindowsVolume()
    {
        var runner = new FakeRunner();
        var mounter = new FakeMounter();
        var engine = CreateEngine(
            runner,
            VerificationFiles(),
            mounter);

        var result = await engine.RepairAsync(
            ValidReport("S", null),
            false,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.Success, result.Outcome);
        Assert.Equal(1, runner.Calls);
        Assert.Equal(1, mounter.AssignCalls);
        Assert.Equal(1, mounter.RemoveCalls);
        Assert.Contains(
            @"\\?\Volume{windows}\",
            mounter.AssignedVolumes);
    }

    [Fact]
    public async Task RealModeFailureWhenRunnerFails()
    {
        var runner = new FakeRunner
        {
            Result = new CommandResult(
                "bcdboot",
                Array.Empty<string>(),
                1,
                string.Empty,
                "failed",
                false,
                false,
                TimeSpan.Zero)
        };
        var engine = CreateEngine(runner, VerificationFiles());

        var result = await engine.RepairAsync(
            ValidReport("S"),
            false,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task RepairBlockedDoesNotInvokeRunner()
    {
        var runner = new FakeRunner();
        var engine = CreateEngine(runner, VerificationFiles());
        var valid = ValidReport("S");
        var report = new DiagnosticReport
        {
            Firmware = new(
                FirmwareMode.LegacyBios,
                "test",
                string.Empty),
            WindowsCandidates = valid.WindowsCandidates,
            SelectedWindows = valid.SelectedWindows,
            EfiCandidates = valid.EfiCandidates,
            SelectedEfi = valid.SelectedEfi
        };

        var result = await engine.RepairAsync(
            report,
            false,
            null,
            CancellationToken.None);

        Assert.Equal(RepairOutcome.Failed, result.Outcome);
        Assert.Equal(0, runner.Calls);
    }

    private static RepairEngine CreateEngine(
        FakeRunner runner,
        FakeFileSystem fileSystem,
        FakeMounter? mounter = null)
    {
        var firmware = new FirmwareDetector(
            new FakeRegistry { Value = 2 },
            new NullApi());
        var volumes = new FakeVolumes();
        var environment = new FakeEnvironment
        {
            SystemRoot = @"C:\Windows"
        };
        var diagnostics = new Diagnostics(
            firmware,
            new WinPeDetector(
                new FakeRegistry { MiniNt = true },
                environment),
            new WindowsInstallationFinder(volumes, fileSystem),
            new EfiPartitionFinder(volumes, fileSystem));
        return new RepairEngine(
            runner,
            new BackupManager(fileSystem),
            new Verifier(fileSystem),
            diagnostics,
            firmware,
            mounter ?? new FakeMounter(),
            environment,
            fileSystem);
    }

    private static DiagnosticReport ValidReport(
        string? efiLetter,
        string? windowsLetter = "D")
    {
        var windows = windowsLetter is null
            ? new WindowsInstallation(
                null,
                @"\\?\Volume{windows}\",
                @"\\?\Volume{windows}\",
                Array.Empty<string>(),
                Array.Empty<string>())
            : new WindowsInstallation(
                windowsLetter,
                $@"{windowsLetter}:\",
                @"\\?\Volume{windows}\",
                Array.Empty<string>(),
                Array.Empty<string>());
        var candidate = new EfiPartitionCandidate(
            new VolumeInfo(
                efiLetter,
                @"\\?\Volume{esp}\",
                "FAT32",
                100 * 1024 * 1024,
                EfiPartitionFinder.EspType,
                true,
                string.Empty),
            50,
            Array.Empty<string>());
        return new DiagnosticReport
        {
            Firmware = new(
                FirmwareMode.Uefi,
                "test",
                string.Empty),
            WindowsCandidates = new[] { windows },
            SelectedWindows = windows,
            EfiCandidates = new[] { candidate },
            SelectedEfi = candidate
        };
    }

    private static FakeFileSystem VerificationFiles()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files.Add(
            @"S:\EFI\Microsoft\Boot\bootmgfw.efi");
        fileSystem.Files.Add(@"S:\EFI\Microsoft\Boot\BCD");
        fileSystem.Files.Add(
            @"S:\EFI\Microsoft\Boot\bootmgr.efi");
        fileSystem.Files.Add(@"S:\EFI\Boot\bootx64.efi");
        fileSystem.Lengths[@"S:\EFI\Microsoft\Boot\BCD"] = 5;
        return fileSystem;
    }
}
