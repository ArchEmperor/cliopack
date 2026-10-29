using Cliopack.Clio;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Packages;

public sealed class PackagePane : FrameView
{
    readonly PackageBrowser browser;
    readonly PackageTable table;
    readonly Label status = new() { Y = Pos.AnchorEnd(), Width = Dim.Fill(), Height = Dim.Auto() };
    CancellationTokenSource? loading;

    public PackagePane(PackageBrowser browser)
    {
        this.browser = browser;
        Label searchLabel = new() { Text = "_Search:" };
        TextField search = new() { X = Pos.Right(searchLabel) + 1, Width = Dim.Fill(), Text = browser.Filter.Search };
        search.ValueChanged += (_, e) =>
        {
            browser.Filter.Search = e.NewValue ?? "";
            Refilter();
        };
        table = new PackageTable(browser) { Y = 1, Width = Dim.Fill(), Height = Dim.Fill(to: status) };
        table.SelectionChanged += UpdateTitle;
        table.HighlightChanged += () => HighlightChanged?.Invoke();
        status.TextFormatter.WordWrap = true;
        Add(searchLabel, search, table, status);
        Show(browser.Environment is null ? "Pick an environment and press Enter." : "");
    }

    // Also raised when the highlighted package got new details.
    public event Action? HighlightChanged;

    // Cancels the previous load; a cancelled or superseded load leaves no trace.
    public async void Load(string environment, bool refresh)
    {
        loading?.Cancel();
        loading = new CancellationTokenSource();
        var ct = loading.Token;
        var load = browser.LoadAsync(environment, refresh, ct);
        Show($"Loading packages from {environment}...");
        var message = "";
        try
        {
            await load;
            if (browser.DetailsPending)
            {
                Show("Loading modified dates and dependencies...");
                await browser.LoadDetailsAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (ClioException e)
        {
            message = e.Message;
        }
        if (!ct.IsCancellationRequested)
            Show(message);
    }

    public void Reload()
    {
        if (browser.Environment is { } environment)
            Load(environment, refresh: true);
    }

    public void ToggleSystem()
    {
        browser.Filter.ShowSystem = !browser.Filter.ShowSystem;
        Refilter();
    }

    public void SortByNext()
    {
        browser.Filter.SortByNext(withModified: browser.Packages is { Detailed: true });
        Refilter();
    }

    // Returns the names this environment does not have.
    public IReadOnlyList<string> Select(IReadOnlyList<string> names)
    {
        var missing = browser.SelectOnly(names);
        Refilter();
        return missing;
    }

    public void Reverse()
    {
        browser.Filter.Descending = !browser.Filter.Descending;
        Refilter();
    }

    void Refilter()
    {
        browser.Refilter();
        table.Refresh();
        UpdateTitle();
    }

    void Show(string message)
    {
        status.Text = message;
        table.Refresh();
        UpdateTitle();
    }

    void UpdateTitle()
    {
        var system = browser.Filter.ShowSystem ? "" : ", system hidden";
        Title = browser.Packages is { } list
            ? $"Packages: {browser.Environment} [{list.Mode}] - {browser.Visible.Count} of {list.Items.Count}{system}, {browser.SelectedCount} selected"
            : browser.Environment is { } env ? $"Packages: {env}" : "Packages";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            loading?.Cancel();
        base.Dispose(disposing);
    }
}
