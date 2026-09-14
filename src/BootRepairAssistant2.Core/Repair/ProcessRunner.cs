using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BootRepairAssistant2.Core;

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly Regex SafeName =
        new("^[A-Za-z0-9_.:/\\\\\\- ]+$", RegexOptions.Compiled);

    public async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (!SafeName.IsMatch(fileName))
        {
            throw new ArgumentException(
                "Executable path contains unsupported characters.",
                nameof(fileName));
        }

        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var timedOut = false;
        var cancelled = false;
        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            cancelled = ct.IsCancellationRequested;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
        }

        stopwatch.Stop();
        return new CommandResult(
            fileName,
            args,
            timedOut || cancelled ? null : process.ExitCode,
            await stdout,
            await stderr,
            timedOut,
            cancelled,
            stopwatch.Elapsed);
    }
}
