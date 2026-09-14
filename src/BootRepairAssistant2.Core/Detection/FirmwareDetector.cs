namespace BootRepairAssistant2.Core;

public sealed class FirmwareDetector
{
    private readonly IRegistryReader registry;
    private readonly IFirmwareApi api;
    private readonly ILogSink log;

    public FirmwareDetector(
        IRegistryReader registry,
        IFirmwareApi api,
        ILogSink? log = null)
    {
        this.registry = registry;
        this.api = api;
        this.log = log ?? NullLogSink.Instance;
    }

    public FirmwareDetectionResult Detect()
    {
        var rawValue = registry.GetValue(
            @"SYSTEM\CurrentControlSet\Control",
            "PEFirmwareType");
        var registryResult = Interpret(rawValue);

        if (registryResult.Mode != FirmwareMode.Unknown)
        {
            log.Log($"Firmware: {registryResult.Details}");
            return registryResult;
        }

        var apiValue = api.GetFirmwareType();
        var apiResult = InterpretApi(apiValue);
        if (apiResult.Mode != FirmwareMode.Unknown)
        {
            var result = apiResult with
            {
                Source = "GetFirmwareType API",
                Details = $"{apiResult.Details}; registry {registryResult.Details}"
            };
            log.Log($"Firmware: {result.Details}");
            return result;
        }

        return new FirmwareDetectionResult(
            FirmwareMode.Unknown,
            "None",
            $"{registryResult.Details}; GetFirmwareType API unavailable or returned an unexpected value ({(apiValue.HasValue ? apiValue.Value.ToString() : "missing")}).");
    }

    public static FirmwareDetectionResult Interpret(object? rawValue)
    {
        return rawValue switch
        {
            int value => FromValue(value, "Registry PEFirmwareType"),
            uint value => FromValue(value, "Registry PEFirmwareType"),
            long value => FromValue(value, "Registry PEFirmwareType"),
            _ => new FirmwareDetectionResult(
                FirmwareMode.Unknown,
                "Registry PEFirmwareType",
                rawValue is null
                    ? "PEFirmwareType value missing"
                    : $"unexpected value type {rawValue.GetType().Name}")
        };
    }

    private static FirmwareDetectionResult InterpretApi(uint? value)
    {
        return value.HasValue
            ? FromValue(value.Value, "GetFirmwareType API")
            : new FirmwareDetectionResult(
                FirmwareMode.Unknown,
                "GetFirmwareType API",
                "API unavailable");
    }

    private static FirmwareDetectionResult FromValue(
        long value,
        string source)
    {
        return value switch
        {
            1 => new FirmwareDetectionResult(
                FirmwareMode.LegacyBios,
                source,
                $"{source} = 1 (BIOS)"),
            2 => new FirmwareDetectionResult(
                FirmwareMode.Uefi,
                source,
                $"{source} = 2 (UEFI)"),
            _ => new FirmwareDetectionResult(
                FirmwareMode.Unknown,
                source,
                $"{source} unexpected value {value}")
        };
    }
}
