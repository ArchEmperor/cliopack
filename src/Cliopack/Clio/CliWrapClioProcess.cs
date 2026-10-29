using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CliWrap;

namespace Cliopack.Clio;

public sealed class CliWrapClioProcess(string executable = "clio") : IClioProcess
{
    public async Task<ClioRun> RunAsync(IReadOnlyList<string> args, Action<string>? onLine, CancellationToken ct)
    {
        StringBuilder output = new(), error = new();
        try
        {
            // Cancelling ct kills the whole process tree (CliWrap forceful cancellation).
            var result = await Cli.Wrap(executable)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Collect(output, line, onLine)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Collect(error, line, onLine)))
                .ExecuteAsync(ct);
            return new ClioRun(result.ExitCode, output.ToString(), error.ToString());
        }
        catch (Win32Exception e)
        {
            throw new ClioNotFoundException(e);
        }
    }

    // CliWrap always redirects stdio, so a full-screen child needs a plain Process.
    public int RunInteractive(IReadOnlyList<string> args)
    {
        ProcessStartInfo info = new(executable, args) { UseShellExecute = false };
        // Ctrl+C belongs to the child; without this it would kill cliopack too.
        ConsoleCancelEventHandler keepAlive = (_, e) => e.Cancel = true;
        Console.CancelKeyPress += keepAlive;
        try
        {
            using var process = Process.Start(info)!;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception e)
        {
            throw new ClioNotFoundException(e);
        }
        finally
        {
            Console.CancelKeyPress -= keepAlive;
        }
    }

    static void Collect(StringBuilder buffer, string line, Action<string>? onLine)
    {
        buffer.AppendLine(line);
        onLine?.Invoke(line);
    }
}
