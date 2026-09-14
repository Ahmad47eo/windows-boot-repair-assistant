using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class WindowsInstallationFinderTests
{
    private static (FakeVolumes Volumes, FakeFileSystem Fs) Windows(string drive, bool kernel = true)
    {
        var volumes = new FakeVolumes { Volumes = new[] { new VolumeInfo(drive, $@"\\?\{drive}:\", "NTFS", 1000, null, false, "") } };
        var fs = new FakeFileSystem(); var root = drive + @":\Windows"; fs.Directories.Add(Path.Combine(root, @"System32\")); if (kernel) fs.Files.Add(Path.Combine(root, @"System32\ntoskrnl.exe")); fs.Files.Add(Path.Combine(root, @"System32\config\SYSTEM")); fs.Files.Add(Path.Combine(root, @"System32\winload.efi")); return (volumes, fs);
    }
    [Fact] public void FindsWindowsOnDNotC() { var (v, fs) = Windows("D"); var found = new WindowsInstallationFinder(v, fs).Find("X"); Assert.Single(found); Assert.Equal("D", found[0].DriveLetter); Assert.True(found[0].IsValid); }
    [Fact] public void MissingKernelIsInvalid() { var (v, fs) = Windows("D", false); Assert.False(new WindowsInstallationFinder(v, fs).Find("X")[0].IsValid); }
    [Fact] public void SkipsWinPeRamdisk() { var (v, fs) = Windows("X"); Assert.Empty(new WindowsInstallationFinder(v, fs).Find("X")); }
}

public sealed class EfiPartitionFinderTests
{
    [Fact] public void GptEspWinsOverLetteredDataVolume()
    {
        var esp = new VolumeInfo(null, @"\\?\Volume{esp}\", "FAT32", 100 * 1024 * 1024, EfiPartitionFinder.EspType, true, "");
        var data = new VolumeInfo("S", @"\\?\Volume{data}\", "FAT32", 100 * 1024 * 1024, Guid.NewGuid(), false, "");
        var fs = new FakeFileSystem(); fs.Directories.Add(@"\\?\Volume{esp}\EFI\Microsoft\Boot"); fs.Directories.Add(@"S:\EFI\Microsoft\Boot");
        var result = new EfiPartitionFinder(new FakeVolumes { Volumes = new[] { data, esp } }, fs).Find(); Assert.Equal(EfiPartitionFinder.EspType, result[0].Volume.GptPartitionType);
    }
    [Fact] public void NtfsIsExcluded() { var ntfs = new VolumeInfo("S", @"\\?\Volume{n}\", "NTFS", 100 * 1024 * 1024, EfiPartitionFinder.EspType, true, ""); Assert.Empty(new EfiPartitionFinder(new FakeVolumes { Volumes = new[] { ntfs } }, new FakeFileSystem()).Find()); }
    [Fact] public void IdenticalCandidatesAreAmbiguous() { var a = new VolumeInfo("S", @"\\?\Volume{a}\", "FAT32", 100 * 1024 * 1024, EfiPartitionFinder.EspType, true, ""); var b = a with { DriveLetter = "T", VolumeGuidPath = @"\\?\Volume{b}\" }; var report = new DiagnosticReport { Firmware = new(FirmwareMode.Uefi, "test", ""), WindowsCandidates = new[] { new WindowsInstallation("D", @"D:\Windows", Array.Empty<string>(), Array.Empty<string>()) }, EfiCandidates = new EfiPartitionFinder(new FakeVolumes { Volumes = new[] { a, b } }, new FakeFileSystem()).Find(), SelectedWindows = new WindowsInstallation("D", @"D:\Windows", Array.Empty<string>(), Array.Empty<string>()) }; Assert.False(report.RepairAllowed); Assert.Contains("ambiguous", report.RepairBlockReason, StringComparison.OrdinalIgnoreCase); }
}
