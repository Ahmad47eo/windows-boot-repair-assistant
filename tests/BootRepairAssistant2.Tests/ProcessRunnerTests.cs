using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task EchoCapturesOutput()
    {
        var fileName = OperatingSystem.IsWindows()
            ? "cmd.exe"
            : "/bin/echo";
        var args = OperatingSystem.IsWindows()
            ? new[] { "/c", "echo", "hello" }
            : new[] { "hello" };
        var result = await new ProcessRunner().RunAsync(
            fileName,
            args,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task SleepTimesOut()
    {
        var fileName = OperatingSystem.IsWindows()
            ? "powershell.exe"
            : "/bin/sleep";
        var args = OperatingSystem.IsWindows()
            ? new[] { "-NoProfile", "-Command", "Start-Sleep -Seconds 5" }
            : new[] { "5" };
        var result = await new ProcessRunner().RunAsync(
            fileName,
            args,
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None);

        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task InvalidFilenameThrows()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProcessRunner().RunAsync(
                "bad;command",
                Array.Empty<string>(),
                TimeSpan.FromSeconds(1),
                CancellationToken.None));
    }
}
