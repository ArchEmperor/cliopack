namespace Cliopack.Packages;

public enum PackageSort { Name, Maintainer, Version, Modified }

public sealed class PackageFilter
{
    static readonly string[] SystemMaintainers = ["Terrasoft", "Creatio"];

    public string Search { get; set; } = "";
    public bool ShowSystem { get; set; }
    public PackageSort Sort { get; private set; }
    public bool Descending { get; set; }

    // Same column flips the direction, another column starts ascending.
    public void SortBy(PackageSort column)
    {
        Descending = column == Sort && !Descending;
        Sort = column;
    }

    // Modified is skipped where dates are not available (Basic mode).
    public void SortByNext(bool withModified)
    {
        var next = Next(Sort);
        SortBy(next == PackageSort.Modified && !withModified ? Next(next) : next);

        static PackageSort Next(PackageSort sort) => (PackageSort)(((int)sort + 1) % Enum.GetValues<PackageSort>().Length);
    }

    public IReadOnlyList<Package> Apply(IEnumerable<Package> packages)
    {
        var search = Search.Trim();
        var visible = packages.Where(p =>
            (ShowSystem || !SystemMaintainers.Contains(p.Maintainer, StringComparer.OrdinalIgnoreCase))
            && p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        IComparer<Package> order = Comparer<Package>.Create(Compare);
        return [.. Descending ? visible.Order(order).Reverse() : visible.Order(order)];
    }

    int Compare(Package a, Package b)
    {
        var result = Sort switch
        {
            PackageSort.Maintainer => StringComparer.OrdinalIgnoreCase.Compare(a.Maintainer, b.Maintainer),
            PackageSort.Version => ParseVersion(a.Version).CompareTo(ParseVersion(b.Version)),
            PackageSort.Modified => Nullable.Compare(a.ModifiedOn, b.ModifiedOn),
            _ => 0,
        };
        return result != 0 ? result : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    }

    // "7.10.0" must sort after "7.9.0"; unparsable text sorts first.
    static Version ParseVersion(string text) => Version.TryParse(text, out var v) ? v : new Version();
}
