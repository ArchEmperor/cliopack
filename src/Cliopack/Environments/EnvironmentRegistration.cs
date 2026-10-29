namespace Cliopack.Environments;

public sealed record EnvironmentRegistration(string Name, string Url, string Login, string Password)
{
    // Returns a readable problem or null. Existing names are rejected: reg-web-app would silently overwrite them.
    public string? Validate(IEnumerable<ClioEnvironment> existing)
    {
        if (new[] { Name, Url, Login, Password }.Any(string.IsNullOrWhiteSpace))
            return "All fields are required.";
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return "URL must start with http:// or https://";
        if (existing.Any(e => string.Equals(e.Name, Name, StringComparison.OrdinalIgnoreCase)))
            return $"'{Name}' is already registered. Edit it via Manage in clio.";
        return null;
    }

    internal IReadOnlyList<string> ToArgs() => ["reg-web-app", Name, "-u", Url, "-l", Login, "-p", Password];
}
