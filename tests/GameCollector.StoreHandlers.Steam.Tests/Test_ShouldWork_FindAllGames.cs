using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;
using TestUtils;

namespace GameCollector.StoreHandlers.Steam.Tests;

public partial class SteamTests
{
    [Theory, AutoFileSystem]
    public void Test_ShouldWork_FindAllGames(InMemoryFileSystem fs)
    {
        var (handler, basePath, commonPath) = SetupHandler(fs);
        var expectedGames = SetupGames(fs, basePath, commonPath);
        handler.ShouldFindAllGames(expectedGames);
    }

    [Theory, AutoFileSystem]
    public void Test_ShouldWork_FindAllGamesById(InMemoryFileSystem fs)
    {
        var (handler, basePath, commonPath) = SetupHandler(fs);
        var expectedGames = SetupGames(fs, basePath, commonPath).ToArray();
        handler.ShouldFindAllGamesById(expectedGames, game => game.Id);
    }
}
