using System.IO.Abstractions.TestingHelpers;
using GameCollector.RegistryUtils;
using GameCollector.StoreHandlers.EGS.Tests.AutoData;
using TestUtils;

namespace GameCollector.StoreHandlers.EGS.Tests;

public partial class EGSTests
{
    [Theory, EGSAutoData]
    public void Test_ShouldWork_FindAllGames(MockFileSystem fs, InMemoryRegistry registry)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);
        var expectedGames = SetupGames(fs, manifestDir);

        handler.ShouldFindAllGames(expectedGames);
    }

    [Theory, EGSAutoData]
    public void Test_ShouldWork_FindAllGamesById(MockFileSystem fs, InMemoryRegistry registry)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);
        var expectedGames = SetupGames(fs, manifestDir).ToArray();

        handler.ShouldFindAllGamesById(expectedGames, game => game.Id);
    }
}
