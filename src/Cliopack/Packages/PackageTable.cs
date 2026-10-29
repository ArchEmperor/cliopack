using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Cliopack.Packages;

// Checkbox table over browser.Visible. Call Refresh after any browser change.
public sealed class PackageTable : TableView
{
    readonly PackageBrowser browser;

    public PackageTable(PackageBrowser browser)
    {
        this.browser = browser;
        FullRowSelect = true;
        Style.ExpandLastColumn = false;
        // The wrapper hooks this view's events and never unhooks: create it once and keep the rows live.
        Table = new CheckBoxTableSourceWrapperByObject<Package>(this, new VisibleRows(browser), browser.IsSelected, SetSelected);
        ValueChanged += (_, _) => Highlight();
    }

    public event Action? SelectionChanged;
    public event Action? HighlightChanged;

    public void Refresh()
    {
        Update();
        // Update clamps the drawn cursor but leaves Value on a row that may be gone.
        if (Value?.SelectedCell is { } cell && cell.Y >= browser.Visible.Count && browser.Visible.Count > 0)
            SetSelection(cell.X, browser.Visible.Count - 1, false);
        SetNeedsDraw();
        // Rows may have moved under the cursor.
        Highlight();
    }

    void Highlight()
    {
        // No Value until the cursor first moves; it starts on row 0.
        var row = Value?.SelectedCell.Y ?? 0;
        browser.Highlight(row < browser.Visible.Count ? browser.Visible[row] : null);
        HighlightChanged?.Invoke();
    }

    void SetSelected(Package package, bool value)
    {
        browser.SetSelected(package, value);
        SelectionChanged?.Invoke();
    }

    // Before base: TableView handles header clicks itself, so the Activating event never sees them.
    protected override bool OnActivating(CommandEventArgs args) => SortOnHeaderClick(args) || base.OnActivating(args);

    bool SortOnHeaderClick(CommandEventArgs e)
    {
        if (e.Context?.Binding is not MouseBinding { MouseEvent: { Position: { } position } mouse }
            || !mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
            return false;
        ScreenToCell(position, out var header);
        // Column 0 is the checkbox; the wrapper handles it. Modified sorts only once dates are loaded.
        if (header is not (> 0 and var column) || ((PackageSort)(column - 1) == PackageSort.Modified && browser.Packages is not { Detailed: true }))
            return false;
        browser.Filter.SortBy((PackageSort)(column - 1));
        browser.Refilter();
        Refresh();
        return true;
    }

    // Reads browser.Visible on every call, so filtering never needs a new table source.
    sealed class VisibleRows(PackageBrowser browser) : IEnumerableTableSource<Package>
    {
        public string[] ColumnNames =>
            [Header("Name", PackageSort.Name), Header("Maintainer", PackageSort.Maintainer), Header("Version", PackageSort.Version), Header("Modified", PackageSort.Modified)];
        public int Columns => 4;
        public int Rows => browser.Visible.Count;

        public object this[int row, int col]
        {
            get
            {
                var package = browser.Visible[row];
                return col switch
                {
                    0 => package.Name,
                    1 => package.Maintainer,
                    2 => package.Version,
                    _ => package.ModifiedOn?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "",
                };
            }
        }

        public IEnumerable<Package> GetAllObjects() => browser.Visible;
        public Package GetObjectOnRow(int row) => browser.Visible[row];

        string Header(string title, PackageSort sort) =>
            browser.Filter.Sort != sort ? title : title + (browser.Filter.Descending ? " ▼" : " ▲");
    }
}
