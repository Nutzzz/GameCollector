using System.Web;
using GameFinder.RegistryUtils;
using NexusMods.Paths;

namespace GameFinder.StoreHandlers.Origin.Tests;

public partial class OriginTests
{
    private static (OriginHandler handler, AbsolutePath manifestDir) SetupHandler(InMemoryFileSystem fs, InMemoryRegistry registry)
    {
        var manifestDir = OriginHandler.GetManifestDir(fs);
        fs.AddDirectory(manifestDir);

        var handler = new OriginHandler(fs, registry);
        return (handler, manifestDir);
    }

    private static IEnumerable<OriginGame> SetupGames(InMemoryFileSystem fs, AbsolutePath manifestDir)
    {
        var fixture = new Fixture();

        fixture.Customize<OriginGame>(composer => composer
            .FromFactory<string>(id =>
            {
                var installPath = manifestDir.CombineUnchecked(id);
                var manifest = manifestDir.CombineUnchecked($"{id}.mfst");

                fs.AddFile(manifest, $"?id={HttpUtility.UrlEncode(id)}&dipInstallPath={HttpUtility.UrlEncode(installPath.GetFullPath())}");
                return new OriginGame(OriginGameId.From(id), installPath);
            })
            .OmitAutoProperties());

        return fixture.CreateMany<OriginGame>();
    }
}
