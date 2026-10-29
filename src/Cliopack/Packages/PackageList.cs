namespace Cliopack.Packages;

public enum PackageMode { Basic, Gate }

// Mode is derived once per load; features read it and never re-detect. Detailed: Gate details are in Items.
public sealed record PackageList(IReadOnlyList<Package> Items, PackageMode Mode, bool Detailed = false)
{
    static readonly Version MinimumGate = new(2, 0, 0, 41);

    public static PackageList From(IReadOnlyList<Package> items) =>
        new(items, items.Any(IsGate) ? PackageMode.Gate : PackageMode.Basic);

    static bool IsGate(Package p) =>
        p.Name is "cliogate" or "cliogate_netcore" && Version.TryParse(p.Version, out var v) && v >= MinimumGate;
}
