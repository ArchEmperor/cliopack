using Cliopack.Clio;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Environments;

public sealed class EnvironmentPane : FrameView
{
    readonly EnvironmentCatalog catalog;
    readonly TableView table = new() { Width = Dim.Fill(), FullRowSelect = true };
    readonly Label status = new() { Y = Pos.AnchorEnd(), Width = Dim.Fill(), Height = Dim.Auto() };
    CancellationTokenSource? loading;

    public EnvironmentPane(EnvironmentCatalog catalog)
    {
        this.catalog = catalog;
        Title = "Environments";
        table.Height = Dim.Fill(to: status);
        table.Style.ExpandLastColumn = false;
        // Enter or double-click; handled here so Accept does not bubble to the window.
        table.Accepting += (_, e) =>
        {
            e.Handled = true;
            if (Current is { } environment)
                Chosen?.Invoke(environment);
        };
        status.TextFormatter.WordWrap = true;
        Add(table, status);
    }

    public event Action<ClioEnvironment>? Chosen;

    public IReadOnlyList<ClioEnvironment> Environments { get; private set; } = [];

    ClioEnvironment? Current => table.Value?.SelectedCell.Y is { } row && row < Environments.Count ? Environments[row] : null;

    // Puts the cursor on `focus` (else the clio active one); `choose` also raises Chosen for that row.
    public async void Load(string? focus, bool choose)
    {
        loading?.Cancel();
        loading = new CancellationTokenSource();
        var ct = loading.Token;
        status.Text = "Loading environments...";
        try
        {
            Show(await catalog.LoadAsync(ct), focus);
            status.Text = Environments.Count == 0 ? "No environments yet. F2 registers one." : "";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (ClioException e)
        {
            status.Text = e.Message;
            return;
        }
        if (choose && Current is { } environment)
            Chosen?.Invoke(environment);
    }

    void Show(IReadOnlyList<ClioEnvironment> environments, string? focus)
    {
        Environments = environments;
        table.Table = new EnumerableTableSource<ClioEnvironment>(environments, new()
        {
            [" "] = e => e.IsActive ? "*" : "",
            ["Name"] = e => e.Name,
            ["URL"] = e => e.Uri,
        });
        table.SetSelection(0, RowOf(environments.ToList(), focus), false);
        table.Update();
    }

    static int RowOf(List<ClioEnvironment> environments, string? focus)
    {
        var row = environments.FindIndex(e => e.Name == focus);
        return row >= 0 ? row : Math.Max(environments.FindIndex(e => e.IsActive), 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            loading?.Cancel();
        base.Dispose(disposing);
    }
}
