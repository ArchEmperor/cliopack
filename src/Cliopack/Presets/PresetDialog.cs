using System.Collections.ObjectModel;
using System.Text.Json;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Presets;

// Package sets of one environment. Load returns one as Result; save and delete keep the dialog open.
public sealed class PresetDialog : Dialog<IReadOnlyList<string>>
{
    readonly PresetStore store;
    readonly string environment;
    readonly IReadOnlyList<string> selection;
    readonly ObservableCollection<string> rows = [];
    readonly ListView list = new() { Y = 1, Width = Dim.Fill(), Height = 8 };
    readonly TextField name = new() { X = 7, Y = 10, Width = Dim.Fill() };
    readonly Label message = new() { Y = 12, Width = Dim.Fill(), Height = Dim.Auto() };
    readonly Button close = new() { Text = "_Close" };
    readonly Button delete = new() { Text = "_Delete" };
    readonly Button save = new() { Text = "_Save selection" };
    IReadOnlyDictionary<string, string[]> presets = new Dictionary<string, string[]>();

    public PresetDialog(PresetStore store, string environment, IReadOnlyList<string> selection)
    {
        this.store = store;
        this.environment = environment;
        this.selection = selection;
        Title = $"Presets: {environment}";
        Width = Dim.Percent(60);
        list.SetSource(rows);
        list.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } row && row < presets.Count)
                name.Text = presets.Keys.ElementAt(row);
        };
        message.TextFormatter.WordWrap = true;
        Add(new Label { Text = $"{selection.Count} package(s) selected", Width = Dim.Fill() },
            list, new Label { Text = "_Name:", Y = 10 }, name, message);
        AddButton(close);
        AddButton(delete);
        AddButton(save);
        AddButton(new Button { Text = "_Load", IsDefault = true });
        Try(Reload);
    }

    // Enter anywhere or Load picks the named preset; only Close lets the base close the dialog.
    protected override bool OnAccepting(CommandEventArgs args)
    {
        var source = args.Context?.Source?.TryGetTarget(out var view) is true ? view : null;
        if (source == close)
            return base.OnAccepting(args);
        var preset = name.Text.Trim();
        if (source == save)
            Try(() => Save(preset));
        else if (source == delete)
            Try(() => Delete(preset));
        else if (presets.TryGetValue(preset, out var packages))
        {
            Result = packages;
            RequestStop();
        }
        else
            message.Text = preset is "" ? "Pick a preset or type its name." : $"No preset {preset}.";
        return true;
    }

    void Save(string preset)
    {
        if (preset is "" || selection.Count == 0)
        {
            message.Text = preset is "" ? "Type a name." : "Nothing selected: Space marks packages in the list.";
            return;
        }
        var replaced = presets.ContainsKey(preset);
        store.Save(environment, preset, selection);
        Reload();
        message.Text = $"{(replaced ? "Replaced" : "Saved")} {preset}: {selection.Count} package(s).";
    }

    void Delete(string preset)
    {
        if (!presets.ContainsKey(preset))
        {
            message.Text = $"No preset {preset}.";
            return;
        }
        store.Delete(environment, preset);
        Reload();
        message.Text = $"Deleted {preset}.";
    }

    void Reload()
    {
        presets = store.For(environment);
        rows.Clear();
        foreach (var (preset, packages) in presets)
            rows.Add($"{preset} ({packages.Length})");
    }

    // A broken or locked file must not take the app down.
    void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            message.Text = $"{store.FilePath}: {e.Message}";
        }
    }
}
