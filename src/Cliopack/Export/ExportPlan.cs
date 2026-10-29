namespace Cliopack.Export;

public enum ExportKind { Download, Bundle, Unzip }

public enum ConflictChoice { Overwrite, Rename, Skip }

// Name is what the target is staged as; Path is where it lands (differs after Rename).
public sealed record ExportTarget(string Name, string Path, IReadOnlyList<string> Packages)
{
    public bool Exists => File.Exists(Path) || Directory.Exists(Path);
}

public sealed record ExportPlan(
    string Environment, ExportKind Kind, string OutputDir,
    IReadOnlyList<ExportTarget> Targets, IReadOnlyList<string> Skipped)
{
    // Sync pull-pkg is one HTTP call with a 10-min timeout; not measured, kept small on purpose.
    // --async (cliogate) is not used: it failed on a live Gate environment.
    public const int ChunkSize = 10;

    // Compatibility switch: one pull-pkg per package, for environments where batch export fails.
    public bool OneByOne { get; init; }

    public static ExportPlan Create(
        string environment, ExportKind kind, IReadOnlyList<string> packages, string outputDir, string bundleName)
    {
        IReadOnlyList<ExportTarget> targets = kind switch
        {
            ExportKind.Bundle => [Target(BundleFileName(bundleName), packages)],
            ExportKind.Download => [.. packages.Select(p => Target($"{p}.zip", [p]))],
            _ => [.. packages.Select(p => Target(p, [p]))],
        };
        return new(environment, kind, outputDir, targets, []);

        ExportTarget Target(string name, IReadOnlyList<string> names) => new(name, Path.Combine(outputDir, name), names);
    }

    // Returns a readable problem or null.
    public static string? ValidateBundleName(string name) =>
        BundleFileName(name) is ".zip" ? "Bundle name is required."
        : name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "Bundle name has characters a file name cannot have."
        : null;

    public IReadOnlyList<string> Packages => [.. Targets.SelectMany(t => t.Packages)];

    public IReadOnlyList<ExportTarget> Conflicts => [.. Targets.Where(t => t.Exists)];

    public IReadOnlyList<IReadOnlyList<string>> Chunks => [.. Packages.Chunk(OneByOne ? 1 : ChunkSize)];

    // Applied to every conflict at once.
    public ExportPlan Resolve(ConflictChoice choice) => choice switch
    {
        ConflictChoice.Skip => this with
        {
            Targets = [.. Targets.Where(t => !t.Exists)],
            Skipped = [.. Skipped, .. Conflicts.SelectMany(t => t.Packages)],
        },
        ConflictChoice.Rename => this with { Targets = [.. Targets.Select(t => t.Exists ? t with { Path = FreePath(t.Path) } : t)] },
        _ => this,
    };

    static string BundleFileName(string name)
    {
        name = name.Trim();
        return (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name) + ".zip";
    }

    // "A.zip" -> "A (2).zip", folder "A" -> "A (2)".
    static string FreePath(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }
    }
}
