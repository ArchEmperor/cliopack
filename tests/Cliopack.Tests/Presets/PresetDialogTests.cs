using Cliopack.Presets;
using Terminal.Gui.Input;

namespace Cliopack.Tests.Presets;

public sealed class PresetDialogTests : IDisposable
{
    readonly TempDir dir = new();
    readonly PresetStore store;

    public PresetDialogTests() => store = new(dir["presets.json"]);

    public void Dispose() => dir.Dispose();

    [Fact]
    public void Save_keeps_the_dialog_open_and_stores_the_selection()
    {
        using PresetDialog dialog = new(store, "dev", ["A", "B"]);
        using Headless ui = new(dialog);

        ui.Press(Key.N.WithAlt);
        ui.Type("release");
        ui.Press(Key.S.WithAlt);

        Assert.False(dialog.StopRequested);
        Assert.Equal(["A", "B"], store.For("dev")["release"]);
        Assert.Contains(dialog.SubViews, v => v.Text == "Saved release: 2 package(s).");
    }

    [Fact]
    public void Enter_on_the_list_loads_the_highlighted_preset()
    {
        store.Save("dev", "core", ["C"]);
        store.Save("dev", "release", ["A", "B"]);
        using PresetDialog dialog = new(store, "dev", []);
        using Headless ui = new(dialog);

        ui.Press(Key.CursorDown, Key.CursorDown, Key.Enter);

        Assert.True(dialog.StopRequested);
        Assert.Equal(["A", "B"], dialog.Result);
    }

    [Fact]
    public void Delete_removes_the_named_preset()
    {
        store.Save("dev", "release", ["A"]);
        using PresetDialog dialog = new(store, "dev", []);
        using Headless ui = new(dialog);

        ui.Press(Key.N.WithAlt);
        ui.Type("release");
        ui.Press(Key.D.WithAlt);

        Assert.Empty(store.For("dev"));
        Assert.False(dialog.StopRequested);
    }

    [Fact]
    public void A_broken_file_shows_a_message_instead_of_crashing()
    {
        File.WriteAllText(store.FilePath, "{ not json");

        using PresetDialog dialog = new(store, "dev", []);

        Assert.Contains(dialog.SubViews, v => v.Text.StartsWith(store.FilePath));
    }
}
