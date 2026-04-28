using System.Globalization;
using GameFinder.RegistryUtils;
using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;
using TestUtils;

namespace GameFinder.StoreHandlers.GOG.Tests;

public partial class GOGTests
{
    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldError_MissingGameId(InMemoryFileSystem fileSystem, InMemoryRegistry registry, string keyName)
    {
        var (handler, gogKey) = SetupHandler(fileSystem, registry);

        var invalidKey = gogKey.AddSubKey(keyName);

        foreach (var error in handler.ShouldOnlyBeErrors())
        {
            error.Should().BeOneOf($"{invalidKey.GetName()} doesn't have a string value \"gameID\"", "GOG database not found.");
        }
    }

    //[Theory(Skip = "Fix me"), AutoFileSystem]
    [Theory, AutoFileSystem]
    public void Test_ShouldError_MissingGameName(IFileSystem fileSystem, InMemoryRegistry registry, string keyName, long gameId)
    {
        var (handler, gogKey) = SetupHandler(fileSystem, registry);

        var invalidKey = gogKey.AddSubKey(keyName);
        invalidKey.AddValue("gameId", gameId.ToString(CultureInfo.InvariantCulture));

        foreach (var error in handler.ShouldOnlyBeErrors())
        {
            error.Should().BeOneOf($"{invalidKey.GetName()} doesn't have a string value \"gameName\"", "GOG database not found.");
        }
    }

    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldError_MissingPath(InMemoryFileSystem fileSystem, InMemoryRegistry registry, string keyName, long gameId, string gameName)
    {
        var (handler, gogKey) = SetupHandler(fileSystem, registry);

        var invalidKey = gogKey.AddSubKey(keyName);
        invalidKey.AddValue("gameId", gameId.ToString(CultureInfo.InvariantCulture));
        invalidKey.AddValue("gameName", gameName);

        foreach (var error in handler.ShouldOnlyBeErrors())
        {
            error.Should().BeOneOf($"{invalidKey.GetName()} doesn't have a string value \"path\"", "GOG database not found.");
        }
    }
}
