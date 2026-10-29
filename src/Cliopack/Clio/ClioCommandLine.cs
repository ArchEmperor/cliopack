namespace Cliopack.Clio;

public static class ClioCommandLine
{
    static readonly string[] SecretOptions = ["-p", "--password"];

    // The only way to turn args into text: password values are always masked.
    public static string Format(IReadOnlyList<string> args) =>
        string.Join(' ', args.Select((arg, i) => i > 0 && IsSecretOption(args[i - 1]) ? "***" : Quote(arg)));

    static bool IsSecretOption(string arg) => SecretOptions.Contains(arg, StringComparer.OrdinalIgnoreCase);

    static string Quote(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
