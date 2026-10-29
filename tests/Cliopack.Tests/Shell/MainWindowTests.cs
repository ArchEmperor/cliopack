using System.Drawing;
using Cliopack.Clio;
using Cliopack.Environments;
using Cliopack.Export;
using Cliopack.Packages;
using Cliopack.Presets;
using Cliopack.Shell;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Time;

namespace Cliopack.Tests.Shell;

public sealed class MainWindowTests : IDisposable
{
    readonly FakeClioProcess process;
    readonly PackageBrowser browser;
    readonly MainWindow window;

    public MainWindowTests() : this(FakeClioProcess.WithFixtures())
    {
    }

    MainWindowTests(FakeClioProcess process)
    {
        this.process = process;
        ClioCli clio = new(process);
        browser = new PackageBrowser(new PackageCatalog(clio));
        window = new MainWindow(new EnvironmentCatalog(clio), browser, new PackageExporter(clio), new PresetStore(Path.Combine(Path.GetTempPath(), $"cliopack-test-{Guid.NewGuid():N}", "presets.json")));
    }

    DependencyPane Dependencies => window.SubViews.OfType<DependencyPane>().Single();

    public void Dispose() => window.Dispose();

    [Fact]
    public void Esc_closes_main_window()
    {
        using IApplication app = Application.Create(new VirtualTimeProvider()).Init(DriverRegistry.Names.ANSI);
        var iterations = 0;
        app.Iteration += (_, _) =>
        {
            // Hard stop so a broken quit key fails the test instead of hanging it.
            if (++iterations > 50)
                app.RequestStop();
            else
                app.InjectKey(Key.Esc);
        };

        app.Run(window);

        Assert.False(window.IsRunning);
        Assert.True(iterations <= 50, "Esc did not close the window");
    }

    [Fact]
    public void Lists_environments_and_loads_packages_only_on_Enter()
    {
        using Headless ui = new(window);

        Assert.Equal(["list-environments", "--json"], process.Calls.Single());

        ui.Press(Key.Enter);

        Assert.Equal("Creatio", browser.Environment);
        Assert.Equal(5, browser.Visible.Count);
    }

    [Fact]
    public void Search_toggle_and_selection_work_from_the_keyboard()
    {
        using Headless ui = new(window);
        ui.Press(Key.Enter, Key.F6);

        ui.Type("macros");
        Assert.Equal("AcmeCommunicationsMacros", Assert.Single(browser.Visible).Name);

        ui.Press(Key.Tab, Key.Space);
        Assert.Equal(1, browser.SelectedCount);

        ui.Press(Key.F4);
        Assert.Equal(1, browser.SelectedCount);
        Assert.True(browser.Filter.ShowSystem);
    }

    [Fact]
    public void Filtering_out_the_cursor_row_keeps_a_highlight()
    {
        using Headless ui = new(window);
        ui.Press(Key.Enter, Key.F6, Key.Tab, Key.CursorDown, Key.CursorDown, Key.CursorDown, Key.CursorDown);

        ui.Press(Key.Tab.WithShift);
        ui.Type("macros");

        Assert.Equal("AcmeCommunicationsMacros", browser.Highlighted?.Name);
    }

    [Fact]
    public void Header_click_sorts_by_that_column()
    {
        using Headless ui = new(window);
        ui.App.Driver!.SetScreenSize(120, 30);
        ui.Press(Key.Enter);
        ui.App.LayoutAndDraw(true);
        var table = window.SubViews.SelectMany(v => v.SubViews).OfType<PackageTable>().Single();
        // Column 2 is Maintainer: after the checkbox and Name.
        var header = (from y in Enumerable.Range(0, 3) from x in Enumerable.Range(0, table.Viewport.Width) select new Point(x, y))
            .First(p => table.ScreenToCell(p, out var column) is null && column == 2);

        ui.App.InjectMouse(new Mouse { ScreenPosition = table.ViewportToScreen(header), Flags = MouseFlags.LeftButtonClicked });

        Assert.Equal(PackageSort.Maintainer, browser.Filter.Sort);
    }

    [Fact]
    public void F3_closes_the_window_to_hand_over_to_clio()
    {
        using Headless ui = new(window);

        ui.Press(Key.F3);

        Assert.True(window.ManageRequested);
    }

    [Fact]
    public async Task Restart_restores_the_previous_environment()
    {
        await browser.LoadAsync("sample-dev", refresh: false, TestContext.Current.CancellationToken);
        browser.Invalidate();
        process.Calls.Clear();

        using Headless ui = new(window);

        Assert.Equal(["list-environments", "list-packages"], process.Calls.Select(args => args[0]));
        Assert.Equal("sample-dev", process.Calls[1][2]);
    }

    [Fact]
    public void Basic_mode_shows_the_cliogate_hint_for_dependencies()
    {
        using Headless ui = new(window);

        ui.Press(Key.Enter);

        Assert.Contains(Dependencies.SubViews, v => v.Text.StartsWith("Modified dates and dependencies require cliogate"));
    }

    [Fact]
    public void Gate_mode_loads_details_and_shows_dependencies_of_the_highlighted_package()
    {
        using MainWindowTests gate = new(FakeClioProcess.Gate(Fixture.Text("sql-package-details.json")));
        using Headless ui = new(gate.window);

        ui.Press(Key.Enter);

        Assert.Equal(["list-environments", "list-packages", "execute-sql-script"], gate.process.Calls.Select(args => args[0]));
        Assert.Equal("cliogate depends on", gate.Dependencies.Title);
        Assert.Equal(["Base"], gate.browser.Highlighted!.DependsOn);
    }
}
