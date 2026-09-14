using BootRepairAssistant2.Core;

namespace BootRepairAssistant2.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task EchoCapturesOutput()
    {
        var result = await new ProcessRunner().RunAsync(
            "/bin/echo",
            new[] { "hello" },
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task SleepTimesOut()
    {
        var result = await new ProcessRunner().RunAsync(
            "/bin/sleep",
            new[] { "5" },
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
