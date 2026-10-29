using Cliopack.Export;

namespace Cliopack.Script;

public sealed class UsageException(string message) : Exception(message);

// Packages is null when a preset names them.
public sealed record PullOptions(
    string Environment, IReadOnlyList<string>? Packages, string? Preset,
    ExportKind Kind, string BundleName, bool OneByOne, ConflictChoice? OnConflict)
{
    public const string Usage = """
        Usage:
          cliopack                       open the TUI
          cliopack pull -e <env> (-p <A,B,...> | --preset <name>) [options]

        pull options:
          --mode download|bundle|unzip   default download
          --name <bundle>                bundle file name, default bundle
          --one-by-one                   one pull-pkg per package (compatibility, slower)
          --on-conflict overwrite|rename|skip
                                         default: stop when a target exists
        Output goes to the current directory. Exit code 1 when any package failed.
        """;

    public static PullOptions Parse(IReadOnlyList<string> args)
    {
        string? environment = null, packages = null, preset = null, mode = null, name = null, conflict = null;
        var oneByOne = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "-e" or "--environment": environment = Value(ref i); break;
                case "-p" or "--packages": packages = Value(ref i); break;
                case "--preset": preset = Value(ref i); break;
                case "--mode": mode = Value(ref i); break;
                case "--name": name = Value(ref i); break;
                case "--on-conflict": conflict = Value(ref i); break;
                case "--one-by-one": oneByOne = true; break;
                default: throw new UsageException($"Unknown option {args[i]}.");
            }
        }
        if (environment is null)
            throw new UsageException("-e <environment> is required.");
        if ((packages is null) == (preset is null))
            throw new UsageException("Give either -p <A,B,...> or --preset <name>.");
        string[]? names = packages?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names is [])
            throw new UsageException("-p lists no packages.");
        var kind = mode switch
        {
            null or "download" => ExportKind.Download,
            "bundle" => ExportKind.Bundle,
            "unzip" => ExportKind.Unzip,
            _ => throw new UsageException($"Unknown mode {mode}: use download, bundle or unzip."),
        };
        if (name is not null && kind != ExportKind.Bundle)
            throw new UsageException("--name only goes with --mode bundle.");
        if (ExportPlan.ValidateBundleName(name ??= "bundle") is { } problem)
            throw new UsageException(problem);
        ConflictChoice? onConflict = conflict switch
        {
            null => null,
            "overwrite" => ConflictChoice.Overwrite,
            "rename" => ConflictChoice.Rename,
            "skip" => ConflictChoice.Skip,
            _ => throw new UsageException($"Unknown --on-conflict {conflict}: use overwrite, rename or skip."),
        };
        return new(environment, names, preset, kind, name, oneByOne, onConflict);

        string Value(ref int i) => ++i < args.Count ? args[i] : throw new UsageException($"{args[i - 1]} needs a value.");
    }
}
