using Cliopack.Environments;

namespace Cliopack.Tests.Environments;

public class EnvironmentRegistrationTests
{
    static readonly ClioEnvironment[] Existing = [new("Creatio", "http://localhost:8080", "Supervisor", true)];

    [Theory]
    [InlineData("dev", "https://dev.example.com", "Supervisor", "pwd", null)]
    [InlineData("", "https://dev.example.com", "Supervisor", "pwd", "All fields are required.")]
    [InlineData("dev", "https://dev.example.com", "Supervisor", " ", "All fields are required.")]
    [InlineData("dev", "dev.example.com", "Supervisor", "pwd", "URL must start with http:// or https://")]
    [InlineData("dev", "ftp://dev.example.com", "Supervisor", "pwd", "URL must start with http:// or https://")]
    [InlineData("creatio", "http://other", "Supervisor", "pwd", "'creatio' is already registered. Edit it via Manage in clio.")]
    public void Validate(string name, string url, string login, string password, string? problem)
    {
        Assert.Equal(problem, new EnvironmentRegistration(name, url, login, password).Validate(Existing));
    }
}
