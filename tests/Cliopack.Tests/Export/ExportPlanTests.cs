using Cliopack.Export;

namespace Cliopack.Tests.Export;

public sealed class ExportPlanTests : IDisposable
{
    readonly TempDir output = new();

    public void Dispose() => output.Dispose();

    ExportPlan Plan(ExportKind kind, string bundle = "bundle", params string[] packages) =>
        ExportPlan.Create("dev", kind, packages, output.Path, bundle);

    [Fact]
    public void Targets_follow_the_kind()
    {
        Assert.Equal(["A.zip", "B.zip"], Plan(ExportKind.Download, packages: ["A", "B"]).Targets.Select(t => t.Name));
        Assert.Equal(["A", "B"], Plan(ExportKind.Unzip, packages: ["A", "B"]).Targets.Select(t => t.Name));
        var bundle = Assert.Single(Plan(ExportKind.Bundle, bundle: " release.ZIP ", packages: ["A", "B"]).Targets);
        Assert.Equal("release.zip", bundle.Name);
        Assert.Equal(output["release.zip"], bundle.Path);
        Assert.Equal(["A", "B"], bundle.Packages);
    }

    [Theory]
    [InlineData("bundle", null)]
    [InlineData("  ", "Bundle name is required.")]
    [InlineData(".zip", "Bundle name is required.")]
    [InlineData("a/b", "Bundle name has characters a file name cannot have.")]
    public void Bundle_name_is_validated(string name, string? problem) =>
        Assert.Equal(problem, ExportPlan.ValidateBundleName(name));

    [Fact]
    public void Packages_are_pulled_in_chunks()
    {
        var names = Enumerable.Range(1, 25).Select(i => $"P{i}").ToArray();

        Assert.Equal([10, 10, 5], Plan(ExportKind.Download, packages: names).Chunks.Select(c => c.Count));
        Assert.Empty(Plan(ExportKind.Download, packages: []).Chunks);
    }

    [Fact]
    public void One_by_one_pulls_each_package_alone_and_survives_resolve()
    {
        var plan = Plan(ExportKind.Bundle, packages: ["A", "B", "C"]) with { OneByOne = true };

        Assert.Equal([["A"], ["B"], ["C"]], plan.Resolve(ConflictChoice.Skip).Chunks);
    }

    [Fact]
    public void Skip_drops_existing_targets()
    {
        File.WriteAllText(output["A.zip"], "");

        var plan = Plan(ExportKind.Download, packages: ["A", "B"]).Resolve(ConflictChoice.Skip);

        Assert.Equal(["B"], plan.Packages);
        Assert.Equal(["A"], plan.Skipped);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void Rename_picks_the_first_free_name_for_files_and_folders()
    {
        File.WriteAllText(output["A.zip"], "");
        File.WriteAllText(output["A (2).zip"], "");
        Directory.CreateDirectory(output["B"]);

        var download = Plan(ExportKind.Download, packages: ["A"]).Resolve(ConflictChoice.Rename);
        var unzip = Plan(ExportKind.Unzip, packages: ["B"]).Resolve(ConflictChoice.Rename);

        Assert.Equal(output["A (3).zip"], Assert.Single(download.Targets).Path);
        Assert.Equal(output["B (2)"], Assert.Single(unzip.Targets).Path);
        Assert.Equal("B", unzip.Targets[0].Name);
    }

    [Fact]
    public void Overwrite_keeps_targets_as_they_are()
    {
        File.WriteAllText(output["A.zip"], "");
        var plan = Plan(ExportKind.Download, packages: ["A"]);

        Assert.Equal(plan.Targets, plan.Resolve(ConflictChoice.Overwrite).Targets);
    }
}
