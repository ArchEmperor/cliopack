using System.IO.Compression;
using Cliopack.Clio;

namespace Cliopack.Tests;

sealed class FakeClioProcess(Func<IReadOnlyList<string>, Task<ClioRun>> respond) : IClioProcess
{
    public List<IReadOnlyList<string>> Calls { get; } = [];

    public static FakeClioProcess Returning(string output, string error = "", int exitCode = 0) =>
        new(_ => Task.FromResult(new ClioRun(exitCode, output, error)));

    public static FakeClioProcess ReturningFixture(string name) => Returning(Fixture.Text(name));

    // Routes by clio command: list-environments and list-packages answer with fixtures, the rest with `other`.
    public static FakeClioProcess WithFixtures(ClioRun? other = null) => new(args => Task.FromResult(args[0] switch
    {
        "list-environments" => new ClioRun(0, Fixture.Text("list-environments.json"), ""),
        "list-packages" => new ClioRun(0, Fixture.Text("list-packages.json"), ""),
        _ => other ?? new ClioRun(0, "", ""),
    }));

    // pull-pkg writes <Name>.gz holding the name for every requested package except `missing`;
    // extract-pkg-zip makes a <Name>/ folder per .gz.
    public static FakeClioProcess Exporting(params string[] missing) => new(args =>
    {
        if (args[0] == "pull-pkg")
        {
            using var zip = ZipFile.Open(ValueOf(args, "-d"), ZipArchiveMode.Create);
            foreach (var name in args[1].Split(',').Except(missing))
            {
                using var writer = new StreamWriter(zip.CreateEntry($"{name}.gz").Open());
                writer.Write(name);
            }
        }
        if (args[0] == "extract-pkg-zip")
        {
            using var zip = ZipFile.OpenRead(args[1]);
            foreach (var entry in zip.Entries)
                Directory.CreateDirectory(Path.Combine(ValueOf(args, "-d"), Path.GetFileNameWithoutExtension(entry.Name)));
        }
        return Task.FromResult(new ClioRun(0, "Done", ""));
    });

    public static string ValueOf(IReadOnlyList<string> args, string option) => args[args.ToList().IndexOf(option) + 1];

    // A Gate environment: list-packages has cliogate, execute-sql-script writes `rows` to its -d file.
    public static FakeClioProcess Gate(string rows) => new(args =>
    {
        if (args[0] == "execute-sql-script")
            File.WriteAllText(ValueOf(args, "-d"), rows);
        return Task.FromResult(new ClioRun(0, args[0] switch
        {
            "list-environments" => Fixture.Text("list-environments.json"),
            "list-packages" => GatePackages,
            _ => "",
        }, ""));
    });

    // UIds match Fixtures/sql-package-details.json.
    const string GatePackages = """
        {"ok": true, "data": [
          {"Descriptor": {"Name": "cliogate", "UId": "e24226f9-c177-458f-af34-9338e2699983", "Maintainer": "Advanced Technologies Foundation", "PackageVersion": "2.0.0.53"}},
          {"Descriptor": {"Name": "Custom", "UId": "a00051f4-cde3-4f3f-b08e-c5ad1a5c735a", "Maintainer": "Customer", "PackageVersion": ""}},
          {"Descriptor": {"Name": "CrtEngagementInBulkEmail", "UId": "0689cb35-c357-5d16-41a5-f4f474c67440", "Maintainer": "Creatio", "PackageVersion": "7.8.0"}},
          {"Descriptor": {"Name": "Core", "UId": "bf235681-3101-4477-b9c7-bbf4a31205be", "Maintainer": "Terrasoft", "PackageVersion": "8.0.0"}}
        ]}
        """;

    public async Task<ClioRun> RunAsync(IReadOnlyList<string> args, Action<string>? onLine, CancellationToken ct)
    {
        Calls.Add(args);
        var run = await respond(args);
        foreach (var line in run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            onLine?.Invoke(line.TrimEnd('\r'));
        return run;
    }

    public int RunInteractive(IReadOnlyList<string> args)
    {
        Calls.Add(args);
        return respond(args).GetAwaiter().GetResult().ExitCode;
    }
}

static class Fixture
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
