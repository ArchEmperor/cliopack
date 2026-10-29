using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cliopack.Clio;

// The only API features use to run clio.
public sealed partial class ClioCli(IClioProcess process)
{
    public static readonly Version MinimumVersion = new(8, 1);

    // Streams output to onLine, starting with the masked command line.
    public Task<ClioRun> RunAsync(IReadOnlyList<string> args, Action<string> onLine, CancellationToken ct)
    {
        onLine($"> clio {ClioCommandLine.Format(args)}");
        return process.RunAsync(args, onLine, ct);
    }

    // Always adds --json: some commands print secrets in plain text without it.
    // Data is read with the caller's source-generated type, so trimming and AOT keep working.
    public async Task<T> RunJsonAsync<T>(IReadOnlyList<string> args, JsonTypeInfo<T> type, CancellationToken ct)
    {
        var run = await process.RunAsync([.. args, "--json"], null, ct);
        var envelope = TryParse(() => JsonSerializer.Deserialize(run.Output, ClioJson.Default.Envelope));
        if (envelope is { Ok: true, Data: { } data } && TryParse(() => data.Deserialize(type)) is { } result)
            return result;
        throw new ClioException($"clio {args[0]} failed: {envelope?.Error?.Message ?? ErrorText(run)}");
    }

    // Only for commands with a reliable exit code; pull-pkg exits 0 on failure.
    public async Task RunCheckedAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var run = await process.RunAsync(args, null, ct);
        if (run.ExitCode != 0)
            throw new ClioException($"clio {args[0]} failed: {ErrorText(run)}");
    }

    // Needs cliogate. No envelope and stdout mixes in [INF] lines, so the raw JSON row array is read from the -d file.
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, string environment, JsonTypeInfo<List<T>> type, CancellationToken ct)
    {
        var file = Path.GetTempFileName();
        try
        {
            await RunCheckedAsync(["execute-sql-script", sql, "-e", environment, "-v", "json", "-d", file, "--silent"], ct);
            return JsonSerializer.Deserialize(File.ReadAllText(file), type) ?? [];
        }
        catch (JsonException e)
        {
            throw new ClioException($"clio execute-sql-script returned unreadable JSON: {e.Message}", e);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public int RunInteractive(IReadOnlyList<string> args) => process.RunInteractive(args);

    // Returns null when clio is usable, otherwise a message with the command that fixes it.
    public async Task<string?> CheckVersionAsync(CancellationToken ct)
    {
        ClioRun run;
        try
        {
            run = await process.RunAsync(["--version"], null, ct);
        }
        catch (ClioNotFoundException e)
        {
            return e.Message;
        }
        var text = run.Output.Trim();
        return Version.TryParse(text, out var version) && version >= MinimumVersion
            ? null
            : $"clio {MinimumVersion} or newer is required, found '{text}'. Update it: dotnet tool update clio -g";
    }

    static T? TryParse<T>(Func<T?> parse)
    {
        try
        {
            return parse();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    // Some failures (e.g. unknown environment) skip the envelope and print "[ERR] - <message>".
    static string ErrorText(ClioRun run)
    {
        var text = (string.IsNullOrWhiteSpace(run.Error) ? run.Output : run.Error).Replace("[ERR] - ", "").Trim();
        return text.Length > 0 ? text : $"exit code {run.ExitCode}";
    }

    sealed record Envelope(bool Ok, JsonElement? Data, EnvelopeError? Error);

    sealed record EnvelopeError(string Code, string Message);

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(Envelope))]
    sealed partial class ClioJson : JsonSerializerContext;
}
