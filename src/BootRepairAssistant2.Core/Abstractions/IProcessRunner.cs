namespace BootRepairAssistant2.Core;

public interface IProcessRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct);
}
