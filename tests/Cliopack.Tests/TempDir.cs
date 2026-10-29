namespace Cliopack.Tests;

sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cliopack-test-{Guid.NewGuid():N}")).FullName;

    public string this[string name] => System.IO.Path.Combine(Path, name);

    public IEnumerable<string> Names => Directory.EnumerateFileSystemEntries(Path).Select(System.IO.Path.GetFileName).Order()!;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
