using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class FirmwareDetectorTests
{
    [Fact] public void PEFirmwareType_2_ReportsUefiDetected() { var result = new FirmwareDetector(new FakeRegistry { Value = 2 }, new NullApi()).Detect(); Assert.Equal(FirmwareMode.Uefi, result.Mode); Assert.StartsWith("UEFI detected", StatusText.ForFirmware(result)); }
    [Fact] public void RegistryOneReportsLegacy() { Assert.Equal(FirmwareMode.LegacyBios, new FirmwareDetector(new FakeRegistry { Value = 1 }, new NullApi()).Detect().Mode); }
    [Fact] public void NullAndApiNullReportsBothFailures() { var result = new FirmwareDetector(new FakeRegistry(), new NullApi()).Detect(); Assert.Equal(FirmwareMode.Unknown, result.Mode); Assert.Contains("PEFirmwareType", result.Details); Assert.Contains("API", result.Details); }
    [Fact] public void ApiTwoReportsUefi() { var result = new FirmwareDetector(new FakeRegistry(), new FakeApi { Value = 2 }).Detect(); Assert.Equal(FirmwareMode.Uefi, result.Mode); Assert.Contains("API", result.Source); }
    [Fact] public void UnexpectedIntegerIsUnknown() => Assert.Equal(FirmwareMode.Unknown, new FirmwareDetector(new FakeRegistry { Value = 7 }, new NullApi()).Detect().Mode);
    [Fact] public void StringTwoIsNotParsed() => Assert.Equal(FirmwareMode.Unknown, new FirmwareDetector(new FakeRegistry { Value = "2" }, new NullApi()).Detect().Mode);
    [Theory]
    [InlineData(null, FirmwareMode.Unknown)]
    [InlineData(1, FirmwareMode.LegacyBios)]
    [InlineData(2, FirmwareMode.Uefi)]
    [InlineData(7, FirmwareMode.Unknown)]
    public void InterpretCases(object? raw, FirmwareMode expected) => Assert.Equal(expected, FirmwareDetector.Interpret(raw).Mode);
}
