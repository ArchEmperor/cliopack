using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Time;

// Terminal.Gui shares static state between app instances: headless tests in parallel lose keys now and then.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Cliopack.Tests;

// A started headless session; injected keys are processed synchronously.
sealed class Headless : IDisposable
{
    readonly SessionToken session;

    public Headless(IRunnable runnable) => session = App.Begin(runnable)!;

    public IApplication App { get; } = Application.Create(new VirtualTimeProvider()).Init(DriverRegistry.Names.ANSI);

    public void Press(params Key[] keys)
    {
        foreach (var key in keys)
            App.InjectKey(key);
    }

    public void Type(string text)
    {
        foreach (var c in text)
            App.InjectKey(c);
    }

    public void Dispose()
    {
        App.End(session);
        App.Dispose();
    }
}
