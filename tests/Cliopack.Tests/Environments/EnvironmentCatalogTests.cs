using Cliopack.Clio;
using Cliopack.Environments;

namespace Cliopack.Tests.Environments;

public class EnvironmentCatalogTests
{
    readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Environments_are_parsed_without_secrets()
    {
        var clio = new ClioCli(FakeClioProcess.ReturningFixture("list-environments.json"));

        var envs = await new EnvironmentCatalog(clio).LoadAsync(ct);

        Assert.Equal(
            [new("Creatio", "http://localhost:8080", "Supervisor", true), new("sample-dev", "https://sample.example.com", "Supervisor", false)],
            envs);
    }

    [Fact]
    public async Task Register_runs_reg_web_app()
    {
        var process = FakeClioProcess.Returning("Environment dev was configured...");

        await new EnvironmentCatalog(new ClioCli(process)).RegisterAsync(new("dev", "http://dev", "Supervisor", "s3cret"), ct);

        Assert.Equal(["reg-web-app", "dev", "-u", "http://dev", "-l", "Supervisor", "-p", "s3cret"], process.Calls.Single());
    }

    [Fact]
    public async Task Register_failure_carries_clio_message()
    {
        var clio = new ClioCli(FakeClioProcess.Returning("", "[ERR] - Unable to auto-detect the Creatio runtime", exitCode: 1));

        var e = await Assert.ThrowsAsync<ClioException>(() => new EnvironmentCatalog(clio).RegisterAsync(new("dev", "http://dev", "a", "b"), ct));

        Assert.Equal("clio reg-web-app failed: Unable to auto-detect the Creatio runtime", e.Message);
    }

    [Fact]
    public void ManageInClio_hands_over_to_env_ui()
    {
        var process = FakeClioProcess.Returning("");

        new EnvironmentCatalog(new ClioCli(process)).ManageInClio();

        Assert.Equal(["env-ui"], process.Calls.Single());
    }
}
