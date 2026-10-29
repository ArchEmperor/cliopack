using Cliopack.Shell;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Time;

namespace Cliopack.Tests.Shell;

public sealed class LogDialogTests
{
    [Fact]
    public void Shows_the_log_and_closes_on_Esc_when_done()
    {
        using LogDialog dialog = new("Job", (write, _) =>
        {
            write("one");
            write("two");
            return Task.CompletedTask;
        });
        using Headless ui = new(dialog);

        Assert.Equal(["one", "two"], dialog.Lines);
        Assert.False(dialog.IsJobRunning);

        ui.Press(Key.Esc);
        Assert.True(dialog.StopRequested);
    }

    [Fact]
    public void Esc_cancels_the_job_and_closes_only_after_it_cleaned_up()
    {
        VirtualTimeProvider time = new();
        using IApplication app = Application.Create(time).Init(DriverRegistry.Names.ANSI);
        bool? openDuringCleanup = null;
        LogDialog? dialog = null;
        dialog = new("Job", async (_, ct) =>
        {
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, ct);
            }
            finally
            {
                // Like killing clio: cleanup ends on a later loop iteration, not inside Cancel.
                await Task.Yield();
                openDuringCleanup = dialog!.IsRunning;
            }
        });
        var iterations = 0;
        app.Iteration += (_, _) =>
        {
            // Posted continuations are timed events: they fire only when virtual time moves.
            time.Advance(TimeSpan.FromMilliseconds(10));
            // Hard stop so a stuck dialog fails the test instead of hanging it.
            if (++iterations > 200)
                app.RequestStop();
            else if (iterations == 1)
                app.InjectKey(Key.Esc);
        };

        app.Run(dialog);

        Assert.True(iterations <= 200, "the dialog did not close after cancel");
        Assert.True(openDuringCleanup);
        Assert.Contains("Cancelling...", dialog.Lines);
        dialog.Dispose();
    }
}
