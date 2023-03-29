using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;

namespace GameCollector.Wine.Tests.Bottles;

public partial class BottlesTests
{
    [Theory, AutoFileSystem]
    public void Test_ShouldWork_ValidPrefix(InMemoryFileSystem fs, string bottleName)
    {
        var (prefixDirectory, prefixManager) = SetupValidBottlesPrefix(fs, bottleName);

        prefixManager
            .FindPrefixes()
            .Should()
            .ContainSingle(result => result.IsT0)
            .Which
            .AsT0
            .ConfigurationDirectory
            .Should()
            .Be(prefixDirectory);
    }
}
