using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;

namespace GameFinder.Wine.Tests;

public partial class WineTests
{
    [Theory, AutoFileSystem]
    public void Test_ShouldWork_GetVirtualDrivePath(AbsolutePath prefixDirectory)
    {
        var winePrefix = new WinePrefix { ConfigurationDirectory = prefixDirectory };
        winePrefix.GetVirtualDrivePath().Should().Be(prefixDirectory.Combine("drive_c"));
    }

    [Theory, AutoFileSystem]
    public void Test_ShouldWork_GetSystemRegistryFile(AbsolutePath prefixDirectory)
    {
        var winePrefix = new WinePrefix { ConfigurationDirectory = prefixDirectory };
        winePrefix.GetSystemRegistryFile().Should().Be(prefixDirectory.Combine("system.reg"));
    }

    [Theory, AutoFileSystem]
    public void Test_ShouldWork_GetUserRegistryFile(AbsolutePath prefixDirectory)
    {
        var winePrefix = new WinePrefix { ConfigurationDirectory = prefixDirectory };
        winePrefix.GetUserRegistryFile().Should().Be(prefixDirectory.Combine("user.reg"));
    }
}
