using System.Text.Json.Serialization;
using Cliopack.Clio;

namespace Cliopack.Packages;

// ModifiedOn (UTC) and DependsOn come only from Gate details; null until loaded.
public sealed record Package(
    string Name, Guid UId, string Maintainer, string Version, DateTime? ModifiedOn = null, IReadOnlyList<string>? DependsOn = null);

public sealed partial class PackageCatalog(ClioCli clio)
{
    readonly Dictionary<string, PackageList> cache = [];

    public async Task<PackageList> LoadAsync(string environment, bool refresh, CancellationToken ct)
    {
        if (!refresh && cache.TryGetValue(environment, out var cached))
            return cached;
        var items = await clio.RunJsonAsync(["list-packages", "-e", environment], PackageJson.Default.Items, ct);
        return cache[environment] = PackageList.From([.. items.Select(i => i.Descriptor.ToPackage())]);
    }

    // One query for both columns; quoted identifiers work on MSSQL and PostgreSQL.
    const string DetailsSql = """
        SELECT p."UId", p."ModifiedOn", d."Name" AS "DependsOn" FROM "SysPackage" p
        LEFT JOIN "SysPackageDependency" pd ON pd."SysPackageId" = p."Id"
        LEFT JOIN "SysPackage" d ON d."Id" = pd."DependOnPackageId"
        """;

    // Gate only: adds modified dates and dependencies to a loaded list.
    public async Task<PackageList> LoadDetailsAsync(string environment, PackageList list, CancellationToken ct)
    {
        var rows = (await clio.QueryAsync(DetailsSql.ReplaceLineEndings(" "), environment, PackageJson.Default.DetailRows, ct)).ToLookup(r => r.UId);
        PackageList detailed = list with
        {
            Detailed = true,
            Items = [.. list.Items.Select(p => p with
            {
                ModifiedOn = rows[p.UId].Select(r => r.ModifiedOn).FirstOrDefault(),
                DependsOn = [.. rows[p.UId].Select(r => r.DependsOn).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)],
            })],
        };
        // A refresh may have replaced the list meanwhile; never cache details over a newer list.
        if (cache.TryGetValue(environment, out var cached) && ReferenceEquals(cached, list))
            cache[environment] = detailed;
        return detailed;
    }

    public void ClearCache() => cache.Clear();

    sealed record DetailRow(Guid UId, DateTime? ModifiedOn, string? DependsOn);

    // clio nests package fields under Descriptor.
    sealed record Item(Descriptor Descriptor);

    sealed record Descriptor(string Name, Guid UId, string? Maintainer, string? PackageVersion)
    {
        public Package ToPackage() => new(Name, UId, Maintainer ?? "", PackageVersion ?? "");
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(List<Item>), TypeInfoPropertyName = "Items")]
    [JsonSerializable(typeof(List<DetailRow>), TypeInfoPropertyName = "DetailRows")]
    sealed partial class PackageJson : JsonSerializerContext;
}
