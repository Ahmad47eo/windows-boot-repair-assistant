namespace BootRepairAssistant2.Core;

internal sealed class NullLogSink : ILogSink
{
    public static readonly NullLogSink Instance = new();

    public void Log(string message)
    {
    }
}
