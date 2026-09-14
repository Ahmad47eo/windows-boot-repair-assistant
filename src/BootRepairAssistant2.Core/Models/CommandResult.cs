namespace BootRepairAssistant2.Core;

public record CommandResult(
    string FileName,
    IReadOnlyList<string> Arguments,
    int? ExitCode,
    string StdOut,
    string StdErr,
    bool TimedOut,
    bool Cancelled,
    TimeSpan Duration)
{
    public string DisplayCommand =>
        string.Join(" ", new[] { FileName }.Concat(Arguments).Select(QuoteForDisplay));

    private static string QuoteForDisplay(string value)
    {
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
