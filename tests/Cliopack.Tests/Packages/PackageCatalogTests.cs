using Cliopack.Clio;
using Cliopack.Packages;

namespace Cliopack.Tests.Packages;

public class PackageCatalogTests
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;
    readonly FakeClioProcess process = FakeClioProcess.ReturningFixture("list-packages.json");

    [Fact]
    public async Task Packages_are_parsed_from_descriptors()
    {
        var packages = await new PackageCatalog(new ClioCli(process)).LoadAsync("Creatio", refresh: false, ct);

        Assert.Equal(["list-packages", "-e", "Creatio", "--json"], process.Calls.Single());
        Assert.Equal(278, packages.Items.Count);
        Assert.Equal(PackageMode.Basic, packages.Mode);
        Assert.Contains(new Package("ActionsDashboard", Guid.Parse("ae8519c2-2aac-4a00-aa61-b0ffaac99ea3"), "Creatio", "7.8.0"), packages.Items);
        Assert.Equal(5, packages.Items.Count(p => p.Maintainer == "Customer"));
    }

    [Fact]
    public async Task Cache_is_per_environment_until_refresh_or_clear()
    {
        PackageCatalog catalog = new(new ClioCli(process));

        var first = await catalog.LoadAsync("Creatio", refresh: false, ct);
        Assert.Same(first, await catalog.LoadAsync("Creatio", refresh: false, ct));
        await catalog.LoadAsync("Other", refresh: false, ct);
        await catalog.LoadAsync("Creatio", refresh: true, ct);
        catalog.ClearCache();
        await catalog.LoadAsync("Creatio", refresh: false, ct);

        Assert.Equal(["Creatio", "Other", "Creatio", "Creatio"], process.Calls.Select(args => args[2]));
    }

    [Fact]
    public async Task Details_add_dates_and_sorted_dependencies_and_replace_the_cache()
    {
        PackageCatalog catalog = new(new ClioCli(FakeClioProcess.Gate(Fixture.Text("sql-package-details.json"))));
        var list = await catalog.LoadAsync("dev", refresh: false, ct);

        var detailed = await catalog.LoadDetailsAsync("dev", list, ct);

        Assert.Equal(PackageMode.Gate, detailed.Mode);
        Assert.True(detailed.Detailed);
        Assert.Equal(new DateTime(2025, 11, 7, 18, 15, 12), detailed.Items.Single(p => p.Name == "Custom").ModifiedOn);
        Assert.Equal(["CrtBulkEmail", "CrtWebTrackingBase"], detailed.Items.Single(p => p.Name == "CrtEngagementInBulkEmail").DependsOn);
        Assert.Empty(detailed.Items.Single(p => p.Name == "Core").DependsOn!);
        Assert.Same(detailed, await catalog.LoadAsync("dev", refresh: false, ct));
    }

    [Fact]
    public async Task Details_never_replace_a_newer_list()
    {
        PackageCatalog catalog = new(new ClioCli(FakeClioProcess.Gate(Fixture.Text("sql-package-details.json"))));
        var old = await catalog.LoadAsync("dev", refresh: false, ct);
        var fresh = await catalog.LoadAsync("dev", refresh: true, ct);

        await catalog.LoadDetailsAsync("dev", old, ct);

        Assert.Same(fresh, await catalog.LoadAsync("dev", refresh: false, ct));
    }
}
