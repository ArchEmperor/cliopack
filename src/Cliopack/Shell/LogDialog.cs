using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Cliopack.Shell;

// Runs a cancellable job and shows its live log. Esc or Cancel stops the job; the dialog stays until it has cleaned up.
public sealed class LogDialog : Dialog
{
    readonly Func<Action<string>, CancellationToken, Task> job;
    readonly ListView log = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    readonly Button close = new() { Text = "_Cancel" };
    readonly CancellationTokenSource cancel = new();
    readonly ConcurrentQueue<string> pending = new();
    readonly ObservableCollection<string> lines = [];
    bool started, finished;

    public LogDialog(string title, Func<Action<string>, CancellationToken, Task> job)
    {
        this.job = job;
        Title = title;
        Width = Dim.Percent(90);
        Height = Dim.Percent(80);
        log.SetSource(lines);
        Add(log);
        AddButton(close);
    }

    public IReadOnlyList<string> Lines => lines;

    public bool IsJobRunning => started && !finished;

    // Started here, not in the ctor: awaits need the UI SynchronizationContext.
    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        if (newIsRunning && !started)
            RunAsync();
        base.OnIsRunningChanged(newIsRunning);
    }

    protected override bool OnIsRunningChanging(bool oldIsRunning, bool newIsRunning)
    {
        if (!newIsRunning && IsJobRunning && !cancel.IsCancellationRequested)
        {
            Write("Cancelling...");
            cancel.Cancel();
        }
        // Cancel can finish the job inline; only a job still running keeps the dialog open.
        return (!newIsRunning && IsJobRunning) || base.OnIsRunningChanging(oldIsRunning, newIsRunning);
    }

    async void RunAsync()
    {
        started = true;
        try
        {
            await job(Write, cancel.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Write(e.Message);
        }
        finished = true;
        close.Text = "_Close";
        // The user asked to close; the job has cleaned up by now.
        if (cancel.IsCancellationRequested)
            RequestStop();
    }

    // Lines come from clio's stdout and stderr threads; one queue keeps them in order.
    void Write(string line)
    {
        pending.Enqueue(line);
        App?.Invoke(Flush);
    }

    void Flush()
    {
        while (pending.TryDequeue(out var line))
            lines.Add(line);
        log.MoveEnd();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            cancel.Dispose();
        base.Dispose(disposing);
    }
}
