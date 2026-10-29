using Cliopack.Packages;

namespace Cliopack.Tests.Packages;

public class PackageFilterTests
{
    static readonly Package Alpha = new("Alpha", Guid.NewGuid(), "Customer", "1.10.0");
    static readonly Package beta = new("beta", Guid.NewGuid(), "Partner", "1.9.0");
    static readonly Package Core = new("Core", Guid.NewGuid(), "Terrasoft", "8.0.0");
    static readonly Package Base = new("Base", Guid.NewGuid(), "creatio", "");
    static readonly Package[] All = [Core, beta, Base, Alpha];

    [Fact]
    public void Hides_Terrasoft_and_Creatio_by_default_and_sorts_by_name()
    {
        Assert.Equal([Alpha, beta], new PackageFilter().Apply(All));
    }

    [Fact]
    public void ShowSystem_shows_every_maintainer()
    {
        Assert.Equal([Alpha, Base, beta, Core], new PackageFilter { ShowSystem = true }.Apply(All));
    }

    [Theory]
    [InlineData("ALP")]
    [InlineData(" lph ")]
    public void Search_is_case_insensitive_substring(string search)
    {
        Assert.Equal([Alpha], new PackageFilter { Search = search }.Apply(All));
    }

    [Fact]
    public void Version_sorts_numerically_and_unparsable_first()
    {
        PackageFilter filter = new() { ShowSystem = true };
        filter.SortBy(PackageSort.Version);

        Assert.Equal([Base, beta, Alpha, Core], filter.Apply(All));
    }

    [Fact]
    public void SortBy_same_column_flips_direction_and_new_column_starts_ascending()
    {
        PackageFilter filter = new();

        filter.SortBy(PackageSort.Name);
        Assert.True(filter.Descending);
        Assert.Equal([beta, Alpha], filter.Apply(All));

        filter.SortBy(PackageSort.Maintainer);
        Assert.False(filter.Descending);
        Assert.Equal(PackageSort.Maintainer, filter.Sort);
    }

    [Fact]
    public void SortByNext_cycles_columns()
    {
        PackageFilter filter = new();

        filter.SortByNext(withModified: false);
        filter.SortByNext(withModified: false);
        Assert.Equal(PackageSort.Version, filter.Sort);
        filter.SortByNext(withModified: false);
        Assert.Equal(PackageSort.Name, filter.Sort);
    }

    [Fact]
    public void Modified_sorts_by_date_with_unknown_first_and_is_skipped_without_dates()
    {
        Package unknown = new("Unknown", Guid.NewGuid(), "Customer", "1.0");
        Package older = Alpha with { ModifiedOn = new DateTime(2024, 1, 1) }, newer = beta with { ModifiedOn = new DateTime(2025, 1, 1) };
        PackageFilter filter = new();

        filter.SortBy(PackageSort.Version);
        filter.SortByNext(withModified: false);
        Assert.Equal(PackageSort.Name, filter.Sort);

        filter.SortBy(PackageSort.Version);
        filter.SortByNext(withModified: true);
        Assert.Equal(PackageSort.Modified, filter.Sort);
        Assert.Equal([unknown, older, newer], filter.Apply([newer, unknown, older]));
    }
}
