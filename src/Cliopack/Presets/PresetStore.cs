using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cliopack.Presets;

// Named package sets per environment in one JSON file. Read on every call, so two running cliopacks keep each other's edits.
public sealed partial class PresetStore(string filePath)
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cliopack", "presets.json");

    public string FilePath => filePath;

    // Sorted by preset name.
    public IReadOnlyDictionary<string, string[]> For(string environment) =>
        Read().TryGetValue(environment, out var presets) ? presets : new SortedDictionary<string, string[]>();

    public void Save(string environment, string name, IReadOnlyList<string> packages) =>
        Change(environment, presets => presets[name] = [.. packages]);

    public void Delete(string environment, string name) => Change(environment, presets => presets.Remove(name));

    Dictionary<string, SortedDictionary<string, string[]>> Read() => File.Exists(filePath)
        ? JsonSerializer.Deserialize(File.ReadAllText(filePath), PresetJson.Default.Presets) ?? []
        : [];

    void Change(string environment, Action<SortedDictionary<string, string[]>> change)
    {
        var all = Read();
        if (!all.TryGetValue(environment, out var presets))
            all[environment] = presets = [];
        change(presets);
        if (presets.Count == 0)
            all.Remove(environment);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(all, PresetJson.Default.Presets));
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Dictionary<string, SortedDictionary<string, string[]>>), TypeInfoPropertyName = "Presets")]
    sealed partial class PresetJson : JsonSerializerContext;
}
