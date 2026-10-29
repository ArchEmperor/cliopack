using Cliopack.Clio;
using Cliopack.Export;
using Cliopack.Presets;
using Cliopack.Script;

namespace Cliopack.Tests.Script;

public sealed class PullCommandTests : IDisposable
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;
    readonly TempDir output = new();
    readonly TempDir config = new();
    readonly StringWriter stdout = new();
    readonly StringWriter stderr = new();
    readonly PresetStore presets;

    public PullCommandTests() => presets = new(config["presets.json"]);

    public void Dispose()
    {
        output.Dispose();
        config.Dispose();
    }

    Task<int> Run(FakeClioProcess clio, params string[] args) =>
        new PullCommand(new PackageExporter(new ClioCli(clio)), presets).RunAsync(PullOptions.Parse(args), output.Path, stdout, stderr, ct);

    [Fact]
    public async Task All_ok_exits_with_0_and_a_failed_package_with_1()
    {
        Assert.Equal(0, await Run(FakeClioProcess.Exporting(), "-e", "dev", "-p", "A,B"));
        Assert.Equal(["A.zip", "B.zip"], output.Names);

        Assert.Equal(1, await Run(FakeClioProcess.Exporting("Gone"), "-e", "dev", "-p", "C,Gone", "--mode", "bundle"));
        Assert.Contains("failed  Gone", stdout.ToString());
    }

    [Fact]
    public async Task Preset_names_the_packages()
    {
        presets.Save("dev", "release", ["A", "B"]);
        var clio = FakeClioProcess.Exporting();

        Assert.Equal(0, await Run(clio, "-e", "dev", "--preset", "release", "--one-by-one"));
        Assert.Equal([["pull-pkg", "A"], ["pull-pkg", "B"]], clio.Calls.Select(c => c.Take(2)));

        Assert.Equal(1, await Run(clio, "-e", "prod", "--preset", "release"));
        Assert.Equal("No preset release for prod.", stderr.ToString().Trim());
    }

    [Fact]
    public async Task Existing_target_stops_unless_a_conflict_choice_is_given()
    {
        File.WriteAllText(output["A.zip"], "old");
        var clio = FakeClioProcess.Exporting();

        Assert.Equal(1, await Run(clio, "-e", "dev", "-p", "A"));
        Assert.Empty(clio.Calls);
        Assert.Contains("Use --on-conflict", stderr.ToString());

        Assert.Equal(0, await Run(clio, "-e", "dev", "-p", "A", "--on-conflict", "skip"));
        Assert.Empty(clio.Calls);
        Assert.Equal(0, await Run(clio, "-e", "dev", "-p", "A", "--on-conflict", "rename"));
        Assert.Equal(["A (2).zip", "A.zip"], output.Names);
    }

    [Fact]
    public async Task Cancel_exits_with_130()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        var code = await new PullCommand(new PackageExporter(new ClioCli(FakeClioProcess.Exporting())), presets)
            .RunAsync(PullOptions.Parse(["-e", "dev", "-p", "A"]), output.Path, stdout, stderr, cancelled.Token);

        Assert.Equal(130, code);
        Assert.Empty(output.Names);
    }
}
