using Cliopack.Clio;
using Cliopack.Packages;

namespace Cliopack.Tests.Packages;

public class PackageBrowserTests
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;

    static PackageBrowser Browser(Func<IReadOnlyList<string>, Task<ClioRun>>? respond = null) =>
        new(new PackageCatalog(new ClioCli(respond is null ? FakeClioProcess.ReturningFixture("list-packages.json") : new FakeClioProcess(respond))));

    [Fact]
    public async Task Selection_survives_filtering_and_sorting()
    {
        var browser = Browser();
        await browser.LoadAsync("Creatio", refresh: false, ct);
        var custom = browser.Visible.Single(p => p.Name == "Custom");

        browser.SetSelected(custom, true);
        browser.Filter.Search = "macros";
        browser.Filter.SortBy(PackageSort.Version);
        browser.Refilter();

        Assert.DoesNotContain(custom, browser.Visible);
        Assert.Equal([custom], browser.Selected);
    }

    [Fact]
    public async Task Select_only_replaces_the_selection_and_reports_missing_names()
    {
        var browser = Browser();
        await browser.LoadAsync("Creatio", refresh: false, ct);
        browser.SetSelected(browser.Visible.Single(p => p.Name == "Custom"), true);

        var missing = browser.SelectOnly(["AcmeCore", "Gone"]);

        Assert.Equal(["Gone"], missing);
        Assert.Equal(["AcmeCore"], browser.Selected.Select(p => p.Name));
    }

    [Fact]
    public async Task Switching_environment_clears_selection_and_refresh_keeps_it()
    {
        var browser = Browser();
        await browser.LoadAsync("Creatio", refresh: false, ct);
        browser.SetSelected(browser.Visible[0], true);

        await browser.LoadAsync("Creatio", refresh: true, ct);
        Assert.Equal(1, browser.SelectedCount);

        await browser.LoadAsync("Other", refresh: false, ct);
        Assert.Equal(0, browser.SelectedCount);
        Assert.Equal("Other", browser.Environment);
    }

    [Fact]
    public async Task Invalidate_drops_lists_but_keeps_environment_and_selection()
    {
        var process = FakeClioProcess.ReturningFixture("list-packages.json");
        PackageBrowser browser = new(new PackageCatalog(new ClioCli(process)));
        await browser.LoadAsync("Creatio", refresh: false, ct);
        browser.SetSelected(browser.Visible[0], true);

        browser.Invalidate();
        Assert.Null(browser.Packages);
        Assert.Empty(browser.Visible);

        await browser.LoadAsync("Creatio", refresh: false, ct);
        Assert.Equal(2, process.Calls.Count);
        Assert.Equal(1, browser.SelectedCount);
    }

    [Fact]
    public async Task Refresh_drops_selected_packages_that_are_gone()
    {
        var calls = 0;
        var browser = Browser(_ => Task.FromResult(new ClioRun(0, ++calls == 1 ? Fixture.Text("list-packages.json") : EmptyList, "")));
        await browser.LoadAsync("Creatio", refresh: false, ct);
        browser.SetSelected(browser.Visible[0], true);

        await browser.LoadAsync("Creatio", refresh: true, ct);

        Assert.Equal(0, browser.SelectedCount);
    }

    [Fact]
    public async Task Late_result_of_a_left_environment_is_ignored()
    {
        TaskCompletionSource<ClioRun> slow = new();
        var browser = Browser(args => args[2] == "Slow" ? slow.Task : Task.FromResult(new ClioRun(0, Fixture.Text("list-packages.json"), "")));

        var stale = browser.LoadAsync("Slow", refresh: false, ct);
        await browser.LoadAsync("Creatio", refresh: false, ct);
        slow.SetResult(new ClioRun(0, EmptyList, ""));
        await stale;

        Assert.Equal("Creatio", browser.Environment);
        Assert.Equal(278, browser.Packages!.Items.Count);
    }

    [Fact]
    public async Task Failed_switch_leaves_an_empty_list_for_the_new_environment()
    {
        var browser = Browser(args => Task.FromResult(args[2] == "Broken" ? new ClioRun(1, "", "[ERR] - boom") : new ClioRun(0, Fixture.Text("list-packages.json"), "")));
        await browser.LoadAsync("Creatio", refresh: false, ct);

        await Assert.ThrowsAsync<ClioException>(() => browser.LoadAsync("Broken", refresh: false, ct));

        Assert.Equal("Broken", browser.Environment);
        Assert.Null(browser.Packages);
        Assert.Empty(browser.Visible);
    }

    [Fact]
    public async Task Gate_details_keep_selection_and_highlight_and_load_once()
    {
        var process = FakeClioProcess.Gate(Fixture.Text("sql-package-details.json"));
        PackageBrowser browser = new(new PackageCatalog(new ClioCli(process)));
        await browser.LoadAsync("dev", refresh: false, ct);
        var custom = browser.Visible.Single(p => p.Name == "Custom");
        browser.SetSelected(custom, true);
        browser.Highlight(custom);
        Assert.True(browser.DetailsPending);

        await browser.LoadDetailsAsync(ct);
        await browser.LoadDetailsAsync(ct);

        Assert.False(browser.DetailsPending);
        Assert.Equal(["CrtCore"], browser.Highlighted!.DependsOn);
        Assert.Equal(["Custom"], browser.Selected.Select(p => p.Name));
        Assert.Single(process.Calls, c => c[0] == "execute-sql-script");
    }

    [Fact]
    public async Task Basic_never_queries_details()
    {
        var process = FakeClioProcess.ReturningFixture("list-packages.json");
        PackageBrowser browser = new(new PackageCatalog(new ClioCli(process)));
        await browser.LoadAsync("Creatio", refresh: false, ct);

        await browser.LoadDetailsAsync(ct);

        Assert.False(browser.DetailsPending);
        Assert.Single(process.Calls);
    }

    const string EmptyList = """{"ok": true, "data": []}""";
}
