namespace Cliopack.Clio;

public sealed record ClioRun(int ExitCode, string Output, string Error);

// The single seam to the clio executable; tests replace it with a fake fed by fixtures.
public interface IClioProcess
{
    // onLine receives stdout and stderr lines as they arrive, possibly from two threads.
    Task<ClioRun> RunAsync(IReadOnlyList<string> args, Action<string>? onLine, CancellationToken ct);

    // Hands the console to clio (inherited stdio) and returns its exit code.
    int RunInteractive(IReadOnlyList<string> args);
}
