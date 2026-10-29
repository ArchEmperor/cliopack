using System.IO.Compression;
using Cliopack.Clio;
using Cliopack.Export;

namespace Cliopack.Tests.Export;

public sealed class PackageExporterTests : IDisposable
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;
    readonly TempDir output = new();
    readonly List<string> log = [];

    public void Dispose() => output.Dispose();

    static Dictionary<string, string> Entries(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        return archive.Entries.ToDictionary(e => e.FullName, e => new StreamReader(e.Open()).ReadToEnd());
    }

    ExportPlan Plan(ExportKind kind, params string[] packages) =>
        ExportPlan.Create("dev", kind, packages, output.Path, "bundle");

    Task<ExportResult> Run(FakeClioProcess clio, ExportPlan plan, CancellationToken? token = null) =>
        new PackageExporter(new ClioCli(clio)).RunAsync(plan, log.Add, token ?? ct);

    [Fact]
    public async Task Download_writes_one_zip_per_package()
    {
        var result = await Run(FakeClioProcess.Exporting(), Plan(ExportKind.Download, packages: ["A", "B"]));

        Assert.True(result.AllOk);
        Assert.Equal(["A.zip", "B.zip"], output.Names);
        Assert.Equal(new Dictionary<string, string> { ["A.gz"] = "A" }, Entries(output["A.zip"]));
        Assert.Equal(new Dictionary<string, string> { ["B.gz"] = "B" }, Entries(output["B.zip"]));
    }

    [Fact]
    public async Task Bundle_joins_all_chunks_into_one_zip()
    {
        var clio = FakeClioProcess.Exporting();
        var names = Enumerable.Range(1, 12).Select(i => $"P{i}").ToArray();

        var result = await Run(clio, Plan(ExportKind.Bundle, packages: names));

        Assert.True(result.AllOk);
        Assert.Equal(2, clio.Calls.Count(c => c[0] == "pull-pkg"));
        Assert.Equal(["bundle.zip"], output.Names);
        Assert.Equal(names.Select(n => $"{n}.gz").Order(), Entries(output["bundle.zip"]).Keys.Order());
    }

    [Fact]
    public async Task Unzip_extracts_each_chunk_into_package_folders()
    {
        var clio = FakeClioProcess.Exporting();

        var result = await Run(clio, Plan(ExportKind.Unzip, packages: ["A", "B"]));

        Assert.True(result.AllOk);
        Assert.Equal(["A", "B"], output.Names);
        Assert.Single(clio.Calls, c => c[0] == "extract-pkg-zip");
    }

    [Fact]
    public async Task Package_missing_from_the_zip_fails_and_the_rest_lands()
    {
        var result = await Run(FakeClioProcess.Exporting("Gone"), Plan(ExportKind.Download, packages: ["A", "Gone"]));

        Assert.False(result.AllOk);
        Assert.Equal(["A.zip"], output.Names);
        Assert.Equal("not in the downloaded zip, see the log", result.Outcomes.Single(o => o.Package == "Gone").Error);
        Assert.Contains("  failed  Gone: not in the downloaded zip, see the log", log);
    }

    [Fact]
    public async Task Overwrite_replaces_and_Rename_keeps_the_existing_target()
    {
        File.WriteAllText(output["A.zip"], "old");
        Directory.CreateDirectory(output["B"]);
        File.WriteAllText(output["B/stale.txt"], "old");

        await Run(FakeClioProcess.Exporting(), Plan(ExportKind.Download, packages: ["A"]).Resolve(ConflictChoice.Overwrite));
        await Run(FakeClioProcess.Exporting(), Plan(ExportKind.Unzip, packages: ["B"]).Resolve(ConflictChoice.Rename));

        Assert.Equal(new Dictionary<string, string> { ["A.gz"] = "A" }, Entries(output["A.zip"]));
        Assert.True(File.Exists(output["B/stale.txt"]));
        Assert.True(Directory.Exists(output["B (2)"]));
    }

    [Fact]
    public async Task Skipped_packages_are_reported_but_not_failed()
    {
        File.WriteAllText(output["A.zip"], "old");

        var result = await Run(FakeClioProcess.Exporting(), Plan(ExportKind.Download, packages: ["A", "B"]).Resolve(ConflictChoice.Skip));

        Assert.True(result.AllOk);
        Assert.Equal(["A"], result.Skipped);
        Assert.Equal("old", File.ReadAllText(output["A.zip"]));
        Assert.Contains("  skipped A (target exists)", log);
    }

    [Fact]
    public async Task Cancel_leaves_nothing_in_the_output()
    {
        using CancellationTokenSource cancel = new();
        TaskCompletionSource<ClioRun> download = new();
        cancel.Token.Register(() => download.TrySetCanceled());
        FakeClioProcess clio = new(args =>
        {
            File.WriteAllText(FakeClioProcess.ValueOf(args, "-d"), "half written");
            cancel.Cancel();
            return download.Task;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(clio, Plan(ExportKind.Download, packages: ["A"]), cancel.Token));

        Assert.Empty(output.Names);
    }
}
