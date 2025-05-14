namespace GameFinder.Common;

public class Settings
{
    public bool InstalledOnly { get; set; } = false;
    public bool BaseOnly { get; set; } = false;
    public bool OwnedOnly { get; set; } = false;
    public bool GamesOnly { get; set; } = false;
    public bool StoreOnly { get; set; } = false;
    public bool PlayableOnly { get; set; } = false;
    public bool CompleteOnly { get; set; } = false;
    public bool OfficialOnly { get; set; } = false;

    public Settings(bool installedOnly = false,
                    bool baseOnly = false,
                    bool ownedOnly = false,
                    bool gamesOnly = false,
                    bool storeOnly = false,
                    bool playableOnly = false,
                    bool completeOnly = false,
                    bool officialOnly = false)
    {
        InstalledOnly = installedOnly;
        BaseOnly = baseOnly;
        OwnedOnly = ownedOnly;
        GamesOnly = gamesOnly;
        StoreOnly = storeOnly;
        PlayableOnly = playableOnly;
        CompleteOnly = completeOnly;
        OfficialOnly = officialOnly;
    }
}
