using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class WindowsInstallationFinderTests
{
    [Fact]
    public void FindsWindowsOnDNotC()
    {
        var volumes = new FakeVolumes
        {
            Volumes = new[]
            {
                new VolumeInfo(
                    "D",
                    @"\\?\Volume{d}\",
                    "NTFS",
                    1000,
                    null,
                    false,
                    string.Empty)
            }
        };
        var fileSystem = WindowsFileSystem("D");

        var result = new WindowsInstallationFinder(
            volumes,
            fileSystem).Find("X");

        Assert.Single(result);
        Assert.Equal("D", result[0].DriveLetter);
        Assert.True(result[0].IsValid);
    }

    [Fact]
    public void MissingKernelIsInvalid()
    {
        var fileSystem = WindowsFileSystem("D");
        var kernel = fileSystem.Files.First(file =>
            file.Contains(
                "ntoskrnl.exe",
                StringComparison.OrdinalIgnoreCase));
        fileSystem.Files.Remove(kernel);
        var volumes = new FakeVolumes
        {
            Volumes = new[]
            {
                WindowsVolume("D")
            }
        };

        var result = new WindowsInstallationFinder(
            volumes,
            fileSystem).Find("X");

        Assert.False(result[0].IsValid);
    }

    [Fact]
    public void SkipsWinPeRamdisk()
    {
        var volumes = new FakeVolumes
        {
            Volumes = new[]
            {
                WindowsVolume("X")
            }
        };

        var result = new WindowsInstallationFinder(
            volumes,
            new FakeFileSystem()).Find("X");

        Assert.Empty(result);
    }

    [Fact]
    public void FindsWindowsOnUnletteredVolumeByGuidPath()
    {
        var volume = new VolumeInfo(
            null,
            @"\\?\Volume{windows}\",
            "NTFS",
            1000,
            null,
            false,
            "Windows");
        var fileSystem = new FakeFileSystem();
        fileSystem.Directories.Add(
            @"\\?\Volume{windows}\Windows\System32\");
        fileSystem.Files.Add(
            @"\\?\Volume{windows}\Windows\System32\ntoskrnl.exe");
        fileSystem.Files.Add(
            @"\\?\Volume{windows}\Windows\System32\config\SYSTEM");
        fileSystem.Files.Add(
            @"\\?\Volume{windows}\Windows\System32\winload.efi");

        var result = new WindowsInstallationFinder(
            new FakeVolumes { Volumes = new[] { volume } },
            fileSystem).Find("X");

        Assert.Single(result);
        Assert.Null(result[0].DriveLetter);
        Assert.Equal(
            @"\\?\Volume{windows}\",
            result[0].RootPath);
        Assert.Equal(
            @"\\?\Volume{windows}\",
            result[0].VolumeGuidPath);
        Assert.Equal(
            @"\\?\Volume{windows}\Windows",
            result[0].WindowsPath);
        Assert.True(result[0].IsValid);
    }

    [Fact]
    public void RawVolumeReportsBitLockerProblem()
    {
        var volume = new VolumeInfo(
            "D",
            @"\\?\Volume{raw}\",
            "RAW",
            1000,
            null,
            false,
            string.Empty);
        var finder = new WindowsInstallationFinder(
            new FakeVolumes { Volumes = new[] { volume } },
            new FakeFileSystem());

        finder.Find("X");

        Assert.Contains(
            finder.LastNotes,
            problem => problem.Contains(
                "possibly BitLocker-locked or unformatted",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NullFilesystemReportsBitLockerProblem()
    {
        var volume = new VolumeInfo(
            "D",
            @"\\?\Volume{raw}\",
            null,
            1000,
            null,
            false,
            string.Empty);
        var finder = new WindowsInstallationFinder(
            new FakeVolumes { Volumes = new[] { volume } },
            new FakeFileSystem());

        finder.Find("X");

        Assert.Contains(
            finder.LastNotes,
            problem => problem.Contains(
                "possibly BitLocker-locked or unformatted",
                StringComparison.OrdinalIgnoreCase));
    }

    private static FakeFileSystem WindowsFileSystem(string drive)
    {
        var fileSystem = new FakeFileSystem();
        var root = drive + @":\Windows";
        fileSystem.Directories.Add(Path.Combine(root, @"System32\"));
        fileSystem.Files.Add(Path.Combine(
            root,
            @"System32\ntoskrnl.exe"));
        fileSystem.Files.Add(Path.Combine(
            root,
            @"System32\config\SYSTEM"));
        fileSystem.Files.Add(Path.Combine(
            root,
            @"System32\winload.efi"));
        return fileSystem;
    }

    private static VolumeInfo WindowsVolume(string drive)
    {
        return new VolumeInfo(
            drive,
            $@"\\?\Volume{{{drive}}}\",
            "NTFS",
            1000,
            null,
            false,
            string.Empty);
    }
}

public sealed class EfiPartitionFinderTests
{
    [Fact]
    public void GptEspWinsOverLetteredDataVolume()
    {
        var esp = new VolumeInfo(
            null,
            @"\\?\Volume{esp}\",
            "FAT32",
            100 * 1024 * 1024,
            EfiPartitionFinder.EspType,
            true,
            string.Empty);
        var data = new VolumeInfo(
            "S",
            @"\\?\Volume{data}\",
            "FAT32",
            100 * 1024 * 1024,
            Guid.NewGuid(),
            false,
            string.Empty);
        var fileSystem = new FakeFileSystem();
        fileSystem.Directories.Add(
            @"\\?\Volume{esp}\EFI\Microsoft\Boot");
        fileSystem.Directories.Add(@"S:\EFI\Microsoft\Boot");

        var result = new EfiPartitionFinder(
            new FakeVolumes { Volumes = new[] { data, esp } },
            fileSystem).Find();

        Assert.Equal(
            EfiPartitionFinder.EspType,
            result[0].Volume.GptPartitionType);
    }

    [Fact]
    public void NtfsIsExcluded()
    {
        var ntfs = new VolumeInfo(
            "S",
            @"\\?\Volume{n}\",
            "NTFS",
            100 * 1024 * 1024,
            EfiPartitionFinder.EspType,
            true,
            string.Empty);

        var result = new EfiPartitionFinder(
            new FakeVolumes { Volumes = new[] { ntfs } },
            new FakeFileSystem()).Find();

        Assert.Empty(result);
    }

    [Fact]
    public void IdenticalCandidatesAreAmbiguous()
    {
        var first = Efi("S");
        var second = new EfiPartitionCandidate(
            new VolumeInfo(
                "T",
                @"\\?\Volume{b}\",
                "FAT32",
                100 * 1024 * 1024,
                EfiPartitionFinder.EspType,
                true,
                string.Empty),
            50,
            Array.Empty<string>());
        var candidates = new EfiPartitionFinder(
            new FakeVolumes { Volumes = new[] { first.Volume, second.Volume } },
            new FakeFileSystem()).Find();
        var windows = new WindowsInstallation(
            "D",
            @"D:\Windows",
            Array.Empty<string>(),
            Array.Empty<string>());
        var report = new DiagnosticReport
        {
            Firmware = new(FirmwareMode.Uefi, "test", string.Empty),
            WindowsCandidates = new[] { windows },
            SelectedWindows = windows,
            EfiCandidates = candidates,
            SelectedEfi = candidates[0]
        };

        Assert.False(report.RepairAllowed);
        Assert.Contains(
            "ambiguous",
            report.RepairBlockReason,
            StringComparison.OrdinalIgnoreCase);
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

public sealed class DiagnosticsTests
{
    [Fact]
    public async Task ExaminedVolumesAppearWhenNoWindowsFound()
    {
        var volume = new VolumeInfo(
            "D",
            @"\\?\Volume{data}\",
            "NTFS",
            1000,
            null,
            false,
            "Data");
        var fileSystem = new FakeFileSystem();
        var registry = new FakeRegistry { Value = 2 };
        var environment = new FakeEnvironment
        {
            SystemRoot = @"X:\Windows"
        };
        var firmware = new FirmwareDetector(registry, new NullApi());
        var diagnostics = new Diagnostics(
            firmware,
            new WinPeDetector(registry, environment),
            new WindowsInstallationFinder(
                new FakeVolumes { Volumes = new[] { volume } },
                fileSystem),
            new EfiPartitionFinder(
                new FakeVolumes { Volumes = new[] { volume } },
                fileSystem));

        var report = await diagnostics.RunAsync(
            CancellationToken.None);

        Assert.Contains(
            report.VolumesExamined,
            examined => examined.VolumeGuidPath.Contains(
                "Volume{data}",
                StringComparison.OrdinalIgnoreCase));
        Assert.Single(report.Problems);
    }

    [Fact]
    public void EfiStatusCanBeOkWhenWindowsIsMissing()
    {
        var efi = new EfiPartitionCandidate(
            new VolumeInfo(
                "S",
                @"\\?\Volume{esp}\",
                "FAT32",
                100 * 1024 * 1024,
                EfiPartitionFinder.EspType,
                true,
                string.Empty),
            50,
            Array.Empty<string>());
        var report = new DiagnosticReport
        {
            Firmware = new(
                FirmwareMode.Uefi,
                "test",
                string.Empty),
            WindowsCandidates = Array.Empty<WindowsInstallation>(),
            EfiCandidates = new[] { efi },
            SelectedEfi = efi
        };

        Assert.Equal(StatusLevel.Ok, report.EfiStatus);
    }

    [Fact]
    public void BitLockerNoteDoesNotBlockRepair()
    {
        var windows = new WindowsInstallation(
            "D",
            @"D:\Windows",
            Array.Empty<string>(),
            Array.Empty<string>());
        var efi = new EfiPartitionCandidate(
            new VolumeInfo(
                "S",
                @"\\?\Volume{esp}\",
                "FAT32",
                100 * 1024 * 1024,
                EfiPartitionFinder.EspType,
                true,
                string.Empty),
            100,
            Array.Empty<string>());
        var report = new DiagnosticReport
        {
            Firmware = new(FirmwareMode.Uefi, "test", string.Empty),
            WindowsCandidates = new[] { windows },
            SelectedWindows = windows,
            EfiCandidates = new[] { efi },
            SelectedEfi = efi,
            Notes = new[]
            {
                "Volume D possibly BitLocker-locked or unformatted"
            }
        };

        Assert.True(report.RepairAllowed);
        Assert.Empty(report.Problems);
        Assert.Single(report.Notes);
    }

    [Fact]
    public async Task NonWinPeAddsSafetyNote()
    {
        var registry = new FakeRegistry { Value = 2 };
        var environment = new FakeEnvironment
        {
            SystemRoot = @"C:\Windows"
        };
        var emptyVolumes = new FakeVolumes();
        var diagnostics = new Diagnostics(
            new FirmwareDetector(registry, new NullApi()),
            new WinPeDetector(registry, environment),
            new WindowsInstallationFinder(
                emptyVolumes,
                new FakeFileSystem()),
            new EfiPartitionFinder(
                emptyVolumes,
                new FakeFileSystem()));

        var report = await diagnostics.RunAsync(
            CancellationToken.None);

        Assert.Contains(
            report.Notes,
            note => note.Contains(
                "Windows PE was not detected",
                StringComparison.OrdinalIgnoreCase));
    }
}
