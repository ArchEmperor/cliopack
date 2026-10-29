using Cliopack.Export;
using Terminal.Gui.Input;

namespace Cliopack.Tests.Export;

public sealed class ExportDialogTests : IDisposable
{
    readonly TempDir output = new();
    readonly ExportDialog dialog;

    public ExportDialogTests() => dialog = new("dev", ["A", "B"], output.Path);

    public void Dispose()
    {
        dialog.Dispose();
        output.Dispose();
    }

    [Fact]
    public void Enter_returns_a_download_plan()
    {
        using Headless ui = new(dialog);

        ui.Press(Key.Enter);

        Assert.True(dialog.StopRequested);
        Assert.Equal(ExportKind.Download, dialog.Result!.Kind);
        Assert.Equal(["A.zip", "B.zip"], dialog.Result.Targets.Select(t => t.Name));
        Assert.False(dialog.Result.OneByOne);
    }

    [Fact]
    public void One_by_one_switch_goes_into_the_plan()
    {
        using Headless ui = new(dialog);

        ui.Press(Key.O.WithAlt, Key.Enter);

        Assert.True(dialog.Result!.OneByOne);
    }

    [Fact]
    public void Bundle_needs_a_name()
    {
        using Headless ui = new(dialog);

        ui.Press(Key.B.WithAlt, Key.N.WithAlt, Key.End);
        ui.Press([.. Enumerable.Repeat(Key.Backspace, "bundle".Length)]);
        ui.Press(Key.Enter);

        Assert.False(dialog.StopRequested);
        Assert.Contains(dialog.SubViews, v => v.Text == "Bundle name is required.");

        ui.Type("release");
        ui.Press(Key.Enter);

        Assert.True(dialog.StopRequested);
        Assert.Equal("release.zip", Assert.Single(dialog.Result!.Targets).Name);
    }
}
