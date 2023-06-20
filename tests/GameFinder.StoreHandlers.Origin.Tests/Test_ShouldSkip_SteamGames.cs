using System.Web;
using GameFinder.RegistryUtils;
using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;

namespace GameFinder.StoreHandlers.Origin.Tests;

public partial class OriginTests
{
    [Theory, AutoFileSystem]
    public void Test_ShouldSkip_SteamGames(InMemoryFileSystem fs, InMemoryRegistry registry,
        string manifestName, string id, AbsolutePath installPath)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var manifest = manifestDir.CombineUnchecked($"{manifestName}.mfst");
        fs.AddFile(manifest, $"?id={HttpUtility.UrlEncode(id)}@steam" +
                             $"&dipInstallPath={HttpUtility.UrlEncode(installPath.GetFullPath())}");

        var results = handler.FindAllGames().ToArray();
        results.Should().BeEmpty();
    }
}
