using Cliopack.Environments;
using Cliopack.Export;
using Cliopack.Packages;
using Cliopack.Presets;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Shell;

public sealed class MainWindow : Window
{
    readonly EnvironmentCatalog environments;
    readonly PackageBrowser packages;
    readonly PackageExporter exporter;
    readonly PresetStore presets;
    readonly EnvironmentPane environmentPane;
    readonly PackagePane packagePane;

    public MainWindow(EnvironmentCatalog environments, PackageBrowser packages, PackageExporter exporter, PresetStore presets)
    {
        this.environments = environments;
        this.packages = packages;
        this.exporter = exporter;
        this.presets = presets;
        Title = "cliopack (F6 switches panes)";
        environmentPane = new EnvironmentPane(environments) { Width = Dim.Percent(35), Height = Dim.Percent(40) };
        DependencyPane dependencyPane = new(packages) { Y = Pos.Bottom(environmentPane), Width = Dim.Width(environmentPane), Height = Dim.Fill(1) };
        packagePane = new(packages) { X = Pos.Right(environmentPane), Width = Dim.Fill(), Height = Dim.Fill(1) };
        environmentPane.Chosen += environment => packagePane.Load(environment.Name, refresh: false);
        packagePane.HighlightChanged += dependencyPane.Refresh;
        Add(environmentPane, packagePane, dependencyPane, new StatusBar([
            new Shortcut(Key.F1, "Help", Help),
            new Shortcut(Key.F2, "Register", Register),
            new Shortcut(Key.F3, "Manage in clio", ManageInClio),
            new Shortcut(Key.F4, "System", packagePane.ToggleSystem),
            new Shortcut(Key.F5, "Refresh", packagePane.Reload),
            new Shortcut(Key.F7, "Sort", packagePane.SortByNext),
            new Shortcut(Key.F8, "Reverse", packagePane.Reverse),
            new Shortcut(Key.F9, "Export", Export),
            new Shortcut(Key.F12, "Presets", Presets),
            new Shortcut(Key.Esc, "Quit", () => RequestStop()),
        ]));
    }

    // Set when the window closed to hand the terminal to clio env-ui.
    public bool ManageRequested { get; private set; }

    // Not in the ctor: awaits need the UI SynchronizationContext, installed when the run starts.
    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        // After a clio env-ui round trip, come back to the environment the user had.
        if (newIsRunning)
            environmentPane.Load(packages.Environment, choose: packages.Environment is not null);
        base.OnIsRunningChanged(newIsRunning);
    }

    void Register()
    {
        using RegisterDialog dialog = new(environments, environmentPane.Environments);
        if (App!.Run(dialog) is string name)
            environmentPane.Load(name, choose: true);
    }

    void Export()
    {
        if (packages is not { Environment: { } environment, Selected: { Count: > 0 } selected })
        {
            MessageBox.Query(App!, "Export", "Select packages first: Space marks the highlighted one.", "_OK");
            return;
        }
        using ExportDialog dialog = new(environment, [.. selected.Select(p => p.Name)], System.Environment.CurrentDirectory);
        if (App!.Run(dialog) is not ExportPlan plan)
            return;
        using LogDialog log = new($"Export from {environment}", (write, ct) => exporter.RunAsync(plan, write, ct));
        App.Run(log);
    }

    void Presets()
    {
        if (packages is not { Environment: { } environment, Packages: not null })
        {
            MessageBox.Query(App!, "Presets", "Load an environment first: Enter in the environment list.", "_OK");
            return;
        }
        using PresetDialog dialog = new(presets, environment, [.. packages.Selected.Select(p => p.Name)]);
        if (App!.Run(dialog) is not IReadOnlyList<string> names)
            return;
        if (packagePane.Select(names) is { Count: > 0 } missing)
            MessageBox.Query(App, "Presets", $"Not in {environment}: {string.Join(", ", missing)}", "_OK");
    }

    void Help()
    {
        using Dialog dialog = new() { Title = "Keys" };
        dialog.Add(new Label { Text = HelpText });
        dialog.AddButton(new Button { Text = "_OK", IsDefault = true });
        App!.Run(dialog);
    }

    // Keep in sync with the status bar.
    const string HelpText = """
        Enter      load the highlighted environment
        F6         next pane (Shift+F6 previous)
        Space      select / unselect the highlighted package
        F2         register an environment
        F3         manage environments in clio (env-ui)
        F4         show / hide Terrasoft and Creatio packages
        F5         reload packages
        F7 / F8    next sort column / reverse; a header click sorts too
        F9         export the selected packages
        F12        presets: load, save, delete package sets
        Esc        close a dialog, quit
        """;

    void ManageInClio()
    {
        ManageRequested = true;
        RequestStop();
    }
}
