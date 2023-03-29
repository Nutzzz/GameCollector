using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;

namespace GameCollector.Wine.Tests;

public partial class WineTests
{
    [Theory, AutoFileSystem]
    public void Test_ShouldWork_GetVirtualDrivePath(AbsolutePath prefixDirectory)
    {
        var winePrefix = new WinePrefix(prefixDirectory);
        winePrefix.GetVirtualDrivePath().Should().Be(prefixDirectory.CombineUnchecked("drive_c"));
    }

    [Theory, AutoFileSystem]
    public void Test_ShouldWork_GetSystemRegistryFile(AbsolutePath prefixDirectory)
    {
        var winePrefix = new WinePrefix(prefixDirectory);
        winePrefix.GetSystemRegistryFile().Should().Be(prefixDirectory.CombineUnchecked("system.reg"));
    }

    [Theory, AutoFileSystem]
    public void Test_ShouldWork_GetUserRegistryFile(AbsolutePath prefixDirectory)
    {
        var winePrefix = new WinePrefix(prefixDirectory);
        winePrefix.GetUserRegistryFile().Should().Be(prefixDirectory.CombineUnchecked("user.reg"));
    }
}
