namespace Cliopack.Packages;

// Package pane state. Lives outside views so it survives the TUI restart around clio env-ui.
public sealed class PackageBrowser(PackageCatalog catalog)
{
    readonly HashSet<string> selected = [];
    string? highlighted;

    public PackageFilter Filter { get; } = new();
    public string? Environment { get; private set; }
    public PackageList? Packages { get; private set; }
    public IReadOnlyList<Package> Visible { get; private set; } = [];

    public IReadOnlyList<Package> Selected => [.. Packages?.Items.Where(IsSelected) ?? []];
    public int SelectedCount => selected.Count;

    // Kept by name: the record is replaced when Gate details arrive.
    public Package? Highlighted => Packages?.Items.FirstOrDefault(p => p.Name == highlighted);
    public bool DetailsPending => Packages is { Mode: PackageMode.Gate, Detailed: false };

    public async Task LoadAsync(string environment, bool refresh, CancellationToken ct)
    {
        if (environment != Environment)
        {
            Environment = environment;
            Packages = null;
            selected.Clear();
            highlighted = null;
            Refilter();
        }
        var packages = await catalog.LoadAsync(environment, refresh, ct);
        // A slow load for an environment the user already left must not overwrite the current one.
        if (environment != Environment)
            return;
        Packages = packages;
        selected.IntersectWith(packages.Items.Select(p => p.Name));
        Refilter();
    }

    // Gate only, after LoadAsync: a second clio call (SQL) for modified dates and dependencies.
    public async Task LoadDetailsAsync(CancellationToken ct)
    {
        if (!DetailsPending || Environment is not { } environment || Packages is not { } list)
            return;
        var detailed = await catalog.LoadDetailsAsync(environment, list, ct);
        if (ReferenceEquals(Packages, list))
        {
            Packages = detailed;
            Refilter();
        }
    }

    // Clio settings may have changed (env-ui): drop every loaded list but keep environment and selection.
    public void Invalidate()
    {
        catalog.ClearCache();
        Packages = null;
        Refilter();
    }

    // Call after changing Filter.
    public void Refilter() => Visible = Filter.Apply(Packages?.Items ?? []);

    public void Highlight(Package? package) => highlighted = package?.Name;

    public bool IsSelected(Package package) => selected.Contains(package.Name);

    // Replaces the selection; returns the names this environment does not have.
    public IReadOnlyList<string> SelectOnly(IReadOnlyList<string> names)
    {
        var known = Packages?.Items.Select(p => p.Name).ToHashSet() ?? [];
        selected.Clear();
        selected.UnionWith(names.Where(known.Contains));
        return [.. names.Where(n => !known.Contains(n))];
    }

    public void SetSelected(Package package, bool value)
    {
        if (value)
            selected.Add(package.Name);
        else
            selected.Remove(package.Name);
    }
}
