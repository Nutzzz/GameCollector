using GameFinder.RegistryUtils;
using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;
using TestUtils;

namespace GameFinder.StoreHandlers.EGS.Tests;

public partial class EGSTests
{
    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldWork_FindAllGames(InMemoryFileSystem fs, InMemoryRegistry registry)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);
        var expectedGames = SetupGames(fs, manifestDir);

        handler.ShouldFindAllGames(expectedGames);
    }

    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldWork_FindAllGamesById(InMemoryFileSystem fs, InMemoryRegistry registry)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);
        var expectedGames = SetupGames(fs, manifestDir).ToArray();

        handler.ShouldFindAllGamesById(expectedGames, game => game.CatalogItemId);
    }

    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldWork_FindAllInterfaceGames(InMemoryFileSystem fs, InMemoryRegistry registry)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);
        var expectedGames = SetupGames(fs, manifestDir);

        handler.ShouldFindAllInterfacesGames(expectedGames);
    }
}
