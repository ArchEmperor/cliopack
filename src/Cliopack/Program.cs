using Cliopack.Clio;
using Cliopack.Environments;
using Cliopack.Export;
using Cliopack.Packages;
using Cliopack.Presets;
using Cliopack.Script;
using Cliopack.Shell;
using Terminal.Gui.App;

if (args.Any(a => a is "-h" or "--help"))
{
    Console.WriteLine(PullOptions.Usage);
    return 0;
}
PullOptions? pull;
try
{
    pull = args switch
    {
        [] => null,
        ["pull", .. var rest] => PullOptions.Parse(rest),
        _ => throw new UsageException($"Unknown command {args[0]}."),
    };
}
catch (UsageException e)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine(PullOptions.Usage);
    return 2;
}

var clio = new ClioCli(new CliWrapClioProcess());
if (await clio.CheckVersionAsync(CancellationToken.None) is { } problem)
{
    Console.Error.WriteLine(problem);
    return 1;
}

var exporter = new PackageExporter(clio);
var presets = new PresetStore(PresetStore.DefaultPath);
if (pull is not null)
{
    using CancellationTokenSource cts = new();
    // Ctrl+C kills clio and removes the staging folder instead of ending the process at once.
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    return await new PullCommand(exporter, presets).RunAsync(pull, Environment.CurrentDirectory, Console.Out, Console.Error, cts.Token);
}

var environments = new EnvironmentCatalog(clio);
var browser = new PackageBrowser(new PackageCatalog(clio));
// Terminal.Gui cannot lend the terminal, so the TUI is shut down around clio env-ui.
while (RunShell(environments, browser, exporter, presets))
{
    environments.ManageInClio();
    browser.Invalidate();
}
return 0;

static bool RunShell(EnvironmentCatalog environments, PackageBrowser browser, PackageExporter exporter, PresetStore presets)
{
    using IApplication app = Application.Create().Init();
    using MainWindow window = new(environments, browser, exporter, presets);
    app.Run(window);
    return window.ManageRequested;
}
