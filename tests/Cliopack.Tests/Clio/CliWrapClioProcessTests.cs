using System.Diagnostics;
using Cliopack.Clio;

namespace Cliopack.Tests.Clio;

// Runs real processes (dotnet, ping/sleep), never clio or Creatio.
public class CliWrapClioProcessTests
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Missing_executable_throws_not_found()
    {
        var process = new CliWrapClioProcess("cliopack-no-such-executable");

        await Assert.ThrowsAsync<ClioNotFoundException>(() => process.RunAsync(["--version"], null, ct));
    }

    [Fact]
    public async Task Streams_lines_and_collects_output()
    {
        List<string> lines = [];

        var run = await new CliWrapClioProcess("dotnet").RunAsync(["--version"], lines.Add, ct);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(run.Output.Trim(), Assert.Single(lines));
    }

    [Fact]
    public void RunInteractive_returns_exit_code()
    {
        Assert.Equal(0, new CliWrapClioProcess("dotnet").RunInteractive(["--version"]));
        Assert.Throws<ClioNotFoundException>(() => new CliWrapClioProcess("cliopack-no-such-executable").RunInteractive([]));
    }

    [Fact]
    public async Task Cancel_kills_the_process()
    {
        string[] args = OperatingSystem.IsWindows() ? ["-n", "30", "127.0.0.1"] : ["30"];
        var process = new CliWrapClioProcess(OperatingSystem.IsWindows() ? "ping" : "sleep");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.RunAsync(args, null, cts.Token));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }
}
