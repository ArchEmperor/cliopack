using Cliopack.Clio;

namespace Cliopack.Tests.Clio;

public class ClioCommandLineTests
{
    [Theory]
    [InlineData("-p")]
    [InlineData("--password")]
    [InlineData("--Password")]
    public void Masks_password_value(string option)
    {
        var text = ClioCommandLine.Format(["reg-web-app", "dev", "-l", "Supervisor", option, "s3cret"]);

        Assert.Equal($"reg-web-app dev -l Supervisor {option} ***", text);
    }

    [Fact]
    public void Quotes_args_with_spaces()
    {
        Assert.Equal("pull-pkg A -d \"my dir/a.zip\"", ClioCommandLine.Format(["pull-pkg", "A", "-d", "my dir/a.zip"]));
    }
}
