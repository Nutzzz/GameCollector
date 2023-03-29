using System.Globalization;
using NexusMods.Paths;
using TestUtils;

namespace GameFinder.StoreHandlers.Steam.Tests;

public partial class SteamTests
{
    private static (SteamHandler handler, AbsolutePath basePath, AbsolutePath commonPath) SetupHandler(InMemoryFileSystem fs)
    {
        var defaultSteamDir = SteamHandler.GetDefaultSteamDirectories(fs).First();
        var libraryFoldersFile = SteamHandler.GetLibraryFoldersFile(defaultSteamDir);

        fs.AddFile(libraryFoldersFile, $@"
""libraryfolders""
{{
    ""0""
    {{
        ""path""        ""{defaultSteamDir.GetFullPath().ToEscapedString()}""
    }}
}}");

        var basePath = defaultSteamDir.CombineUnchecked("steamapps");
        var commonPath = basePath.CombineUnchecked("common");

        var handler = new SteamHandler(fs, registry: null);
        return (handler, basePath, commonPath);
    }

    private static IEnumerable<SteamGame> SetupGames(InMemoryFileSystem fs, AbsolutePath basePath, AbsolutePath commonPath)
    {
        var fixture = new Fixture();

        fixture.Customize<SteamGame>(composer => composer
            .FromFactory<int, string>((appId, name) =>
            {
                var path = commonPath.CombineUnchecked(name);

                var manifest = basePath.CombineUnchecked($"{appId.ToString(CultureInfo.InvariantCulture)}.acf");
                fs.AddFile(manifest, @$"
""AppState""
{{
    ""appid""       ""{appId.ToString(CultureInfo.InvariantCulture)}""
    ""name""        ""{name}""
    ""installdir""      ""{name}""
}}");

                return new SteamGame(appId, name, path);
            })
            .OmitAutoProperties());

        return fixture.CreateMany<SteamGame>();
    }

    private static (AbsolutePath protonDirectory, ProtonWinePrefix) SetupProtonPrefix(InMemoryFileSystem fs, int appId, string name)
    {
        var gamePath = fs.GetKnownPath(KnownPath.TempDirectory)
            .CombineUnchecked("common")
            .CombineUnchecked(name);

        var protonDirectory = fs.GetKnownPath(KnownPath.TempDirectory)
            .CombineUnchecked("compatdata")
            .CombineUnchecked(appId.ToString(CultureInfo.InvariantCulture));

        var steamGame = new SteamGame(appId, name, gamePath);

        var protonPrefix = steamGame.GetProtonPrefix();
        return (protonDirectory, protonPrefix);
    }
}
