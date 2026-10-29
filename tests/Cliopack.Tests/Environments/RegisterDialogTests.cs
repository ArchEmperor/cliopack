using Cliopack.Clio;
using Cliopack.Environments;
using Terminal.Gui.Input;

namespace Cliopack.Tests.Environments;

public class RegisterDialogTests
{
    static readonly ClioEnvironment[] Existing = [new("Creatio", "http://localhost:8080", "Supervisor", true)];

    [Fact]
    public void Clio_error_keeps_the_form_open_and_success_returns_the_name()
    {
        var fail = true;
        FakeClioProcess process = new(_ => Task.FromResult(fail ? new ClioRun(1, "", "[ERR] - site unreachable") : new ClioRun(0, "configured", "")));
        using RegisterDialog dialog = new(new EnvironmentCatalog(new ClioCli(process)), Existing);
        using Headless ui = new(dialog);

        Fill(ui, "dev", "http://dev", "Supervisor", "pwd");
        ui.Press(Key.Enter);

        Assert.Equal(["reg-web-app", "dev", "-u", "http://dev", "-l", "Supervisor", "-p", "pwd"], process.Calls.Single());
        Assert.False(dialog.StopRequested);
        Assert.Contains(dialog.SubViews, v => v.Text == "clio reg-web-app failed: site unreachable");

        fail = false;
        ui.Press(Key.Enter);

        Assert.True(dialog.StopRequested);
        Assert.Equal("dev", dialog.Result);
    }

    [Fact]
    public void Invalid_input_never_reaches_clio()
    {
        FakeClioProcess process = FakeClioProcess.Returning("");
        using RegisterDialog dialog = new(new EnvironmentCatalog(new ClioCli(process)), Existing);
        using Headless ui = new(dialog);

        Fill(ui, "Creatio", "http://other", "Supervisor", "pwd");
        ui.Press(Key.Enter);

        Assert.Empty(process.Calls);
        Assert.False(dialog.StopRequested);
    }

    // Leaves focus in the last field, so Enter submits like a user would.
    static void Fill(Headless ui, params string[] values)
    {
        ui.Type(values[0]);
        foreach (var value in values[1..])
        {
            ui.Press(Key.Tab);
            ui.Type(value);
        }
    }
}
