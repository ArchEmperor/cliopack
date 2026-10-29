using System.Text.Json;
using Cliopack.Export;
using Cliopack.Presets;

namespace Cliopack.Script;

// `cliopack pull`: the TUI export for scripts. Returns the process exit code.
public sealed class PullCommand(PackageExporter exporter, PresetStore presets)
{
    public async Task<int> RunAsync(PullOptions options, string outputDir, TextWriter output, TextWriter error, CancellationToken ct)
    {
        try
        {
            return await PullAsync(options, outputDir, output, error, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            error.WriteLine("Cancelled.");
            return 130;
        }
        catch (JsonException e)
        {
            error.WriteLine($"{presets.FilePath}: {e.Message}");
            return 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
    }

    async Task<int> PullAsync(PullOptions options, string outputDir, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var packages = options.Packages;
        if (options.Preset is { } preset)
        {
            if (!presets.For(options.Environment).TryGetValue(preset, out var saved))
            {
                error.WriteLine($"No preset {preset} for {options.Environment}.");
                return 1;
            }
            packages = saved;
        }
        var plan = ExportPlan.Create(options.Environment, options.Kind, packages!, outputDir, options.BundleName) with { OneByOne = options.OneByOne };
        if (plan.Conflicts is { Count: > 0 } conflicts)
        {
            if (options.OnConflict is not { } choice)
            {
                error.WriteLine($"Already exists in {outputDir}: {string.Join(", ", conflicts.Select(c => c.Name))}. Use --on-conflict overwrite|rename|skip.");
                return 1;
            }
            plan = plan.Resolve(choice);
        }
        if (plan.Targets.Count == 0)
        {
            output.WriteLine($"Nothing left to export, skipped: {string.Join(", ", plan.Skipped)}.");
            return 0;
        }
        var result = await exporter.RunAsync(plan, output.WriteLine, ct);
        return result.AllOk ? 0 : 1;
    }
}
