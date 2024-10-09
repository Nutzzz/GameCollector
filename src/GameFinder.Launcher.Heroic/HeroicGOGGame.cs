using GameCollector.StoreHandlers.GOG;
using GameCollector.Wine;
using NexusMods.Paths;

namespace GameCollector.Launcher.Heroic;

public record HeroicGOGGame(
    GOGGameId Id,
    string Name,
    AbsolutePath Path,
    AbsolutePath WinePrefixPath,
    DTOs.WineVersion WineVersion) : GOGGame(Id, Name, Path)
{
    public WinePrefix GetWinePrefix()
    {
        return new WinePrefix
        {
            ConfigurationDirectory = WinePrefixPath.Combine("pfx"),
            UserName = "steamuser",
        };
    }
}