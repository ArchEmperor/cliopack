using System.Text.Json.Serialization;
using Cliopack.Clio;

namespace Cliopack.Environments;

public sealed record ClioEnvironment(string Name, string Uri, string? Login, bool IsActive);

public sealed partial class EnvironmentCatalog(ClioCli clio)
{
    public Task<IReadOnlyList<ClioEnvironment>> LoadAsync(CancellationToken ct) =>
        clio.RunJsonAsync(["list-environments"], EnvironmentJson.Default.Environments, ct);

    public Task RegisterAsync(EnvironmentRegistration registration, CancellationToken ct) =>
        clio.RunCheckedAsync(registration.ToArgs(), ct);

    // Blocks until the user leaves env-ui; the TUI must be shut down before.
    public void ManageInClio() => clio.RunInteractive(["env-ui"]);

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(IReadOnlyList<ClioEnvironment>), TypeInfoPropertyName = "Environments")]
    sealed partial class EnvironmentJson : JsonSerializerContext;
}
