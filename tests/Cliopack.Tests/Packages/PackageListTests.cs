using Cliopack.Packages;

namespace Cliopack.Tests.Packages;

public class PackageListTests
{
    [Theory]
    [InlineData("cliogate", "2.0.0.41", PackageMode.Gate)]
    [InlineData("cliogate_netcore", "2.1.0", PackageMode.Gate)]
    [InlineData("cliogate", "2.0.0.40", PackageMode.Basic)]
    [InlineData("cliogate", "", PackageMode.Basic)]
    [InlineData("cliogate_extra", "9.0.0", PackageMode.Basic)]
    public void Mode_requires_cliogate_2_0_0_41(string name, string version, PackageMode mode)
    {
        var list = PackageList.From([new("Custom", Guid.NewGuid(), "Customer", "1.0"), new(name, Guid.NewGuid(), "Creatio", version)]);

        Assert.Equal(mode, list.Mode);
    }
}
