using NexusMods.Paths;

namespace GameCollector.Wine.Tests;

public partial class WineTests
{
    private static (AbsolutePath prefixDirectory, DefaultWinePrefixManager prefixManager) SetupWinePrefix(InMemoryFileSystem fs)
    {
        var location = DefaultWinePrefixManager
            .GetDefaultWinePrefixLocations(fs)
            .First();

        fs.AddDirectory(location);
        return (location, new DefaultWinePrefixManager(fs));
    }

    private static (AbsolutePath prefixDirectory, DefaultWinePrefixManager prefixManager) SetupValidWinePrefix(InMemoryFileSystem fs, AbsolutePath location)
    {
        fs.AddDirectory(location);
        fs.AddDirectory(location.CombineUnchecked("drive_c"));
        fs.AddEmptyFile(location.CombineUnchecked("system.reg"));
        fs.AddEmptyFile(location.CombineUnchecked("user.reg"));

        return (location, new DefaultWinePrefixManager(fs));
    }
}
