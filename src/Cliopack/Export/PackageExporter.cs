using System.IO.Compression;
using Cliopack.Clio;

namespace Cliopack.Export;

public sealed record PackageOutcome(string Package, string? Path, string? Error);

// Skipped packages are the user's choice, not failures.
public sealed record ExportResult(IReadOnlyList<PackageOutcome> Outcomes, IReadOnlyList<string> Skipped)
{
    public bool AllOk => Outcomes.All(o => o.Error is null);

    public IEnumerable<string> Summary() =>
    [
        $"Done: {Outcomes.Count(o => o.Error is null)} ok, {Outcomes.Count(o => o.Error is not null)} failed, {Skipped.Count} skipped.",
        .. Outcomes.Select(o => o.Error is null ? $"  ok      {o.Package} -> {o.Path}" : $"  failed  {o.Package}: {o.Error}"),
        .. Skipped.Count == 0 ? [] : new[] { $"  skipped {string.Join(", ", Skipped)} (target exists)" },
    ];
}

public sealed class PackageExporter(ClioCli clio)
{
    // Everything is staged next to the output (same volume, so moves are renames) and lands only at the end.
    public async Task<ExportResult> RunAsync(ExportPlan plan, Action<string> log, CancellationToken ct)
    {
        var staging = Directory.CreateDirectory(Path.Combine(plan.OutputDir, $".cliopack-{Guid.NewGuid():N}")).FullName;
        try
        {
            var downloaded = await DownloadAsync(plan, staging, log, ct);
            var staged = Directory.CreateDirectory(Path.Combine(staging, "out")).FullName;
            await StageAsync(plan, downloaded, staged, log, ct);
            ct.ThrowIfCancellationRequested();
            List<PackageOutcome> outcomes = [];
            foreach (var target in plan.Targets)
                outcomes.AddRange(Deliver(plan.Kind, target, downloaded, staged));
            ExportResult result = new(outcomes, plan.Skipped);
            foreach (var line in result.Summary())
                log(line);
            return result;
        }
        finally
        {
            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log($"Could not remove {staging}: {e.Message}");
            }
        }
    }

    // Package name -> chunk zip holding its .gz. pull-pkg exit code is unreliable, so only the zip content counts.
    async Task<Dictionary<string, string>> DownloadAsync(ExportPlan plan, string staging, Action<string> log, CancellationToken ct)
    {
        Dictionary<string, string> downloaded = [];
        var index = 0;
        foreach (var chunk in plan.Chunks)
        {
            var zip = Path.Combine(staging, $"{++index}.zip");
            await clio.RunAsync(["pull-pkg", string.Join(',', chunk), "-e", plan.Environment, "-d", zip], log, ct);
            foreach (var name in PackagesIn(zip).Intersect(chunk))
                downloaded[name] = zip;
        }
        return downloaded;
    }

    async Task StageAsync(ExportPlan plan, Dictionary<string, string> downloaded, string staged, Action<string> log, CancellationToken ct)
    {
        // Repacking runs off the UI thread: big zips would freeze the log dialog and its Cancel.
        switch (plan.Kind)
        {
            case ExportKind.Download:
                await Task.Run(() =>
                {
                    foreach (var (name, zip) in downloaded)
                        Repack(Path.Combine(staged, $"{name}.zip"), [(name, zip)], ct);
                }, ct);
                break;
            case ExportKind.Bundle when downloaded.Count > 0:
                await Task.Run(() => Repack(Path.Combine(staged, plan.Targets.Single().Name), [.. downloaded.Select(d => (d.Key, d.Value))], ct), ct);
                break;
            case ExportKind.Unzip:
                // A fresh folder: clio asks y/n on the console when a package folder already exists.
                foreach (var zip in downloaded.Values.Distinct())
                    await clio.RunAsync(["extract-pkg-zip", zip, "-d", staged], log, ct);
                break;
        }
    }

    static List<PackageOutcome> Deliver(ExportKind kind, ExportTarget target, Dictionary<string, string> downloaded, string staged)
    {
        var source = Path.Combine(staged, target.Name);
        var outcomes = target.Packages.Select(p => new PackageOutcome(p, target.Path, Problem(p))).ToList();
        if (outcomes.Any(o => o.Error is null))
            Move(source, target.Path);
        return outcomes;

        string? Problem(string package) =>
            !downloaded.ContainsKey(package) ? "not in the downloaded zip, see the log"
            : kind == ExportKind.Unzip && !Directory.Exists(Path.Combine(staged, package)) ? "not extracted, see the log"
            : null;
    }

    static IEnumerable<string> PackagesIn(string zip)
    {
        if (!File.Exists(zip) || new FileInfo(zip).Length == 0)
            return [];
        using var archive = ZipFile.OpenRead(zip);
        return [.. archive.Entries.Where(e => e.FullName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            .Select(e => Path.GetFileNameWithoutExtension(e.FullName))];
    }

    // Copies <Name>.gz entries as they are: a package .gz is a Creatio format, not something to recompress.
    static void Repack(string target, IEnumerable<(string Name, string Zip)> packages, CancellationToken ct)
    {
        using var output = ZipFile.Open(target, ZipArchiveMode.Create);
        foreach (var group in packages.GroupBy(p => p.Zip, p => p.Name))
        {
            using var input = ZipFile.OpenRead(group.Key);
            foreach (var name in group)
            {
                ct.ThrowIfCancellationRequested();
                using var from = input.GetEntry($"{name}.gz")!.Open();
                using var to = output.CreateEntry($"{name}.gz", CompressionLevel.NoCompression).Open();
                from.CopyTo(to);
            }
        }
    }

    static void Move(string source, string target)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
        else
            File.Delete(target);
        if (Directory.Exists(source))
            Directory.Move(source, target);
        else
            File.Move(source, target);
    }
}
