using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cliopack.Clio;

namespace Cliopack.Tests.Clio;

public class ClioCliTests
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunJson_always_adds_json_flag()
    {
        var process = FakeClioProcess.ReturningFixture("list-environments.json");

        await new ClioCli(process).RunJsonAsync(["list-environments"], Type<object>(), ct);

        Assert.Equal(["list-environments", "--json"], process.Calls.Single());
    }

    [Fact]
    public async Task RunJson_error_envelope_becomes_readable_message()
    {
        var clio = new ClioCli(FakeClioProcess.ReturningFixture("list-packages-timeout.json"));

        var e = await Assert.ThrowsAsync<ClioException>(() => clio.RunJsonAsync(["list-packages"], Type<object>(), ct));

        Assert.Equal("clio list-packages failed: The operation was canceled. (WebException: Timeout)", e.Message);
    }

    [Fact]
    public async Task RunJson_without_envelope_uses_stderr_text()
    {
        var clio = new ClioCli(FakeClioProcess.Returning("", Fixture.Text("env-not-found.stderr.txt"), exitCode: 1));

        var e = await Assert.ThrowsAsync<ClioException>(() => clio.RunJsonAsync(["list-packages"], Type<object>(), ct));

        Assert.StartsWith("clio list-packages failed: Active environment 'NoSuchEnv' is not found", e.Message);
    }

    [Fact]
    public async Task RunJson_with_no_output_reports_exit_code()
    {
        var clio = new ClioCli(FakeClioProcess.Returning("", exitCode: 3));

        var e = await Assert.ThrowsAsync<ClioException>(() => clio.RunJsonAsync(["list-packages"], Type<object>(), ct));

        Assert.Equal("clio list-packages failed: exit code 3", e.Message);
    }

    [Fact]
    public async Task Run_logs_masked_command_line_first()
    {
        var clio = new ClioCli(FakeClioProcess.Returning("Done"));
        List<string> lines = [];

        await clio.RunAsync(["reg-web-app", "dev", "-p", "s3cret"], lines.Add, ct);

        Assert.Equal(["> clio reg-web-app dev -p ***", "Done"], lines);
    }

    [Theory]
    [InlineData("8.1.0.135\n", true)]
    [InlineData("8.2\n", true)]
    [InlineData("8.0.9.1\n", false)]
    [InlineData("garbage\n", false)]
    public async Task CheckVersion_requires_8_1(string output, bool usable)
    {
        var problem = await new ClioCli(FakeClioProcess.Returning(output)).CheckVersionAsync(ct);

        if (usable)
            Assert.Null(problem);
        else
            Assert.Contains("dotnet tool update clio -g", problem);
    }

    [Fact]
    public async Task CheckVersion_reports_install_command_when_clio_is_missing()
    {
        var clio = new ClioCli(new FakeClioProcess(_ => throw new ClioNotFoundException(new Win32Exception())));

        Assert.Contains("dotnet tool install clio -g", await clio.CheckVersionAsync(ct));
    }

    [Fact]
    public async Task Query_reads_rows_from_the_result_file()
    {
        var process = FakeClioProcess.Gate("""[{"Name": "Custom"}]""");

        var rows = await new ClioCli(process).QueryAsync("SELECT 1", "dev", Type<List<Row>>(), ct);

        Assert.Equal([new Row("Custom")], rows);
        var args = process.Calls.Single();
        Assert.Equal(["execute-sql-script", "SELECT 1", "-e", "dev", "-v", "json", "-d"], args.Take(7));
        Assert.Equal("--silent", args[^1]);
        Assert.False(File.Exists(args[7]));
    }

    [Fact]
    public async Task Query_failure_becomes_readable_message()
    {
        var clio = new ClioCli(FakeClioProcess.Returning("", "[ERR] - ExecuteSQL ERROR: bad column", exitCode: 1));

        var e = await Assert.ThrowsAsync<ClioException>(() => clio.QueryAsync("SELECT x", "dev", Type<List<Row>>(), ct));

        Assert.Equal("clio execute-sql-script failed: ExecuteSQL ERROR: bad column", e.Message);
    }

    sealed record Row(string Name);

    // Reflection is fine in tests; the app passes source-generated types.
    static JsonTypeInfo<T> Type<T>() =>
        (JsonTypeInfo<T>)new JsonSerializerOptions(JsonSerializerOptions.Default) { PropertyNameCaseInsensitive = true }.GetTypeInfo(typeof(T));
}
