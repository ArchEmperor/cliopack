using Cliopack.Export;
using Cliopack.Script;

namespace Cliopack.Tests.Script;

public class PullOptionsTests
{
    [Fact]
    public void Defaults_to_download_without_conflict_choice()
    {
        var options = PullOptions.Parse(["-e", "dev", "-p", "A, B,"]);

        Assert.Equal(new PullOptions("dev", options.Packages, null, ExportKind.Download, "bundle", false, null), options);
        Assert.Equal(["A", "B"], options.Packages);
    }

    [Fact]
    public void Reads_every_option()
    {
        var options = PullOptions.Parse(["--preset", "release", "--mode", "bundle", "--name", "rel", "--one-by-one", "--on-conflict", "rename", "--environment", "dev"]);

        Assert.Equal(new PullOptions("dev", null, "release", ExportKind.Bundle, "rel", true, ConflictChoice.Rename), options);
    }

    [Theory]
    [InlineData("-p A", "-e <environment> is required.")]
    [InlineData("-e dev", "Give either -p <A,B,...> or --preset <name>.")]
    [InlineData("-e dev -p A --preset r", "Give either -p <A,B,...> or --preset <name>.")]
    [InlineData("-e dev -p ,", "-p lists no packages.")]
    [InlineData("-e dev -p A --mode zip", "Unknown mode zip: use download, bundle or unzip.")]
    [InlineData("-e dev -p A --name x", "--name only goes with --mode bundle.")]
    [InlineData("-e dev -p A --mode bundle --name a/b", "Bundle name has characters a file name cannot have.")]
    [InlineData("-e dev -p A --on-conflict ask", "Unknown --on-conflict ask: use overwrite, rename or skip.")]
    [InlineData("-e dev -p A -x", "Unknown option -x.")]
    [InlineData("-e dev -p", "-p needs a value.")]
    public void Bad_input_is_a_usage_error(string args, string message) =>
        Assert.Equal(message, Assert.Throws<UsageException>(() => PullOptions.Parse(args.Split(' '))).Message);
}
