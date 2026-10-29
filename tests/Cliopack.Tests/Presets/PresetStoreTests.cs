using Cliopack.Presets;

namespace Cliopack.Tests.Presets;

public sealed class PresetStoreTests : IDisposable
{
    readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    PresetStore Store() => new(Path.Combine(dir.Path, "config", "presets.json"));

    [Fact]
    public void Missing_file_means_no_presets()
    {
        Assert.Empty(Store().For("dev"));
        Assert.False(Directory.Exists(dir["config"]));
    }

    [Fact]
    public void Presets_are_kept_per_environment_and_sorted()
    {
        Store().Save("dev", "release", ["A", "B"]);
        Store().Save("dev", "core", ["C"]);
        Store().Save("prod", "release", ["D"]);

        Assert.Equal(["core", "release"], Store().For("dev").Keys);
        Assert.Equal(["A", "B"], Store().For("dev")["release"]);
        Assert.Equal(["D"], Store().For("prod")["release"]);
    }

    [Fact]
    public void Save_replaces_and_delete_removes()
    {
        var store = Store();
        store.Save("dev", "release", ["A"]);
        store.Save("dev", "release", ["B"]);
        Assert.Equal(["B"], store.For("dev")["release"]);

        store.Delete("dev", "release");
        Assert.Empty(store.For("dev"));
        Assert.Equal("{}", File.ReadAllText(store.FilePath));
    }
}
