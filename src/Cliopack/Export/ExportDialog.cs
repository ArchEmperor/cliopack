using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Export;

// Picks the export kind and settles existing targets; Result is the plan to run.
public sealed class ExportDialog : Dialog<ExportPlan>
{
    const int ConflictsShown = 8;

    readonly string environment;
    readonly IReadOnlyList<string> packages;
    readonly string outputDir;
    readonly OptionSelector<ExportKind> kind = new() { Y = 2 };
    readonly TextField bundleName = new() { X = 14, Y = 6, Width = Dim.Fill(), Text = "bundle", Enabled = false };
    readonly CheckBox oneByOne = new() { Y = 8, Text = "_One by one: a download per package (compatibility, slower)" };
    readonly Label message = new() { Y = 10, Width = Dim.Fill(), Height = Dim.Auto() };
    readonly Button cancel = new() { Text = "_Cancel" };

    public ExportDialog(string environment, IReadOnlyList<string> packages, string outputDir)
    {
        this.environment = environment;
        this.packages = packages;
        this.outputDir = outputDir;
        Title = "Export";
        Width = Dim.Percent(70);
        kind.Labels = ["_Download: one <Package>.zip per package", "_Bundle: one zip with all packages", "_Unzip: package folders"];
        kind.Value = ExportKind.Download;
        kind.ValueChanged += (_, _) => bundleName.Enabled = kind.Value == ExportKind.Bundle;
        message.TextFormatter.WordWrap = true;
        Add(new Label { Text = $"{packages.Count} package(s) from {environment} to {outputDir}", Width = Dim.Fill() },
            kind, new Label { Text = "Bundle _name:", Y = 6 }, bundleName, oneByOne, message);
        AddButton(cancel);
        AddButton(new Button { Text = "_Export", IsDefault = true });
    }

    // Enter anywhere or Export builds the plan; only Cancel lets the base close the dialog.
    protected override bool OnAccepting(CommandEventArgs args)
    {
        if (args.Context?.Source?.TryGetTarget(out var source) is true && source == cancel)
            return base.OnAccepting(args);
        if (Plan() is { } plan)
        {
            Result = plan;
            RequestStop();
        }
        return true;
    }

    ExportPlan? Plan()
    {
        var selected = kind.Value ?? ExportKind.Download;
        if (selected == ExportKind.Bundle && ExportPlan.ValidateBundleName(bundleName.Text) is { } problem)
        {
            message.Text = problem;
            return null;
        }
        var plan = ExportPlan.Create(environment, selected, packages, outputDir, bundleName.Text) with { OneByOne = oneByOne.Value == CheckState.Checked };
        if (plan.Conflicts is { Count: > 0 } conflicts)
        {
            var names = string.Join(", ", conflicts.Take(ConflictsShown).Select(c => c.Name))
                + (conflicts.Count > ConflictsShown ? $" and {conflicts.Count - ConflictsShown} more" : "");
            if (MessageBox.Query(App!, "Already exists", $"{names} in {outputDir}", "_Overwrite", "_Rename", "_Skip") is not { } choice)
                return null;
            plan = plan.Resolve((ConflictChoice)choice);
        }
        if (plan.Targets.Count == 0)
        {
            message.Text = "Nothing left to export.";
            return null;
        }
        return plan;
    }
}
