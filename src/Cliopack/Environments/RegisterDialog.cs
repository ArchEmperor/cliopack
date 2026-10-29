using Cliopack.Clio;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Environments;

// Stays open while clio registers and on errors; Result is the registered name.
public sealed class RegisterDialog : Dialog<string>
{
    readonly EnvironmentCatalog catalog;
    readonly IReadOnlyList<ClioEnvironment> existing;
    readonly TextField name, url, login, password;
    readonly Label message = new() { Y = 5, Width = Dim.Fill(), Height = Dim.Auto() };
    readonly Button cancel = new() { Text = "_Cancel" };
    readonly Button register = new() { Text = "_Register", IsDefault = true };
    CancellationTokenSource? running;

    public RegisterDialog(EnvironmentCatalog catalog, IReadOnlyList<ClioEnvironment> existing)
    {
        this.catalog = catalog;
        this.existing = existing;
        Title = "Register environment";
        Width = Dim.Percent(70);
        name = AddField("_Name:", 0);
        url = AddField("_URL:", 1);
        login = AddField("_Login:", 2);
        password = AddField("_Password:", 3);
        password.Secret = true;
        message.TextFormatter.WordWrap = true;
        Add(message);
        AddButton(cancel);
        AddButton(register);
    }

    TextField AddField(string label, int row)
    {
        TextField field = new() { X = 11, Y = row, Width = Dim.Fill() };
        Add(new Label { Text = label, Y = row }, field);
        return field;
    }

    // Enter in any field or the Register button submits; only Cancel lets the base close the dialog.
    protected override bool OnAccepting(CommandEventArgs args)
    {
        if (args.Context?.Source?.TryGetTarget(out var source) is true && source == cancel)
            return base.OnAccepting(args);
        if (running is null)
            Submit(new EnvironmentRegistration(name.Text.Trim(), url.Text.Trim(), login.Text.Trim(), password.Text));
        return true;
    }

    async void Submit(EnvironmentRegistration registration)
    {
        if (registration.Validate(existing) is { } problem)
        {
            message.Text = problem;
            return;
        }
        var run = running = new CancellationTokenSource();
        register.Enabled = false;
        message.Text = $"Registering {registration.Name}, clio checks the site...";
        try
        {
            await catalog.RegisterAsync(registration, run.Token);
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            return; // the dialog is closed
        }
        catch (ClioException e)
        {
            running = null;
            register.Enabled = true;
            message.Text = e.Message;
            return;
        }
        Result = registration.Name;
        RequestStop();
    }

    // Esc or Cancel while clio runs: kill it.
    protected override bool OnIsRunningChanging(bool oldIsRunning, bool newIsRunning)
    {
        if (!newIsRunning)
            running?.Cancel();
        return base.OnIsRunningChanging(oldIsRunning, newIsRunning);
    }
}
