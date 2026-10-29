using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Packages;

// What the highlighted package depends on. Display only: nothing here selects packages.
public sealed class DependencyPane : FrameView
{
    readonly PackageBrowser browser;
    readonly Label hint = new() { Width = Dim.Fill(), Height = Dim.Auto() };
    readonly ObservableCollection<string> dependencies = [];

    public DependencyPane(PackageBrowser browser)
    {
        this.browser = browser;
        hint.TextFormatter.WordWrap = true;
        ListView list = new() { Y = Pos.Bottom(hint), Width = Dim.Fill(), Height = Dim.Fill() };
        list.SetSource(dependencies);
        Add(hint, list);
        Refresh();
    }

    public void Refresh()
    {
        var package = browser.Highlighted;
        Title = package is null ? "Depends on" : $"{package.Name} depends on";
        hint.Text = browser.Packages switch
        {
            null => "",
            { Mode: PackageMode.Basic } => $"Modified dates and dependencies require cliogate: clio install-gate -e {browser.Environment}",
            { Detailed: false } => "Not loaded yet, see the status under the packages.",
            _ when package?.DependsOn is [] => "Nothing.",
            _ => "",
        };
        dependencies.Clear();
        foreach (var name in package?.DependsOn ?? [])
            dependencies.Add(name);
    }
}
