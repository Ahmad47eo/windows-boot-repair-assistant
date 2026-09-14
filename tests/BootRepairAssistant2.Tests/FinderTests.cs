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
