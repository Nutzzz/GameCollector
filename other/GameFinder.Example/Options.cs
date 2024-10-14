using CommandLine;

namespace GameCollector;

public class Options
{
    [Option("all", HelpText = "Search for games from all store handlers", Group = "Handlers")]
    public bool All { get; set; } = false;

    [Option("amazon", HelpText = "Search for Amazon games", Hidden = true, Group = "Handlers")]
    public bool Amazon { get; set; } = false;

    [Option("arc", HelpText = "Search for Arc games", Hidden = true, Group = "Handlers")]
    public bool Arc { get; set; } = false;

    // alias for "battlenet"
    [Option("blizzard", HelpText = "Search for Battle.net games", Hidden = true, Group = "Handlers")]
    public bool Blizzard { get; set; } = false;

    [Option("battlenet", HelpText = "Search for Battle.net games", Hidden = true, Group = "Handlers")]
    public bool BattleNet { get; set; } = false;

    [Option("bigfish", HelpText = "Search for Big Fish Game Manager games", Hidden = true, Group = "Handlers")]
    public bool BigFish { get; set; } = false;

    [Option("dolphin", HelpText = "Search for Dolphin ROMs (path to .exe required)", MetaValue = "PATH", Group = "Handlers")]
    public string? Dolphin { get; set; }

    // alias for "ea"
    [Option("ea_desktop", HelpText = "Search for EA app games", Hidden = true)]
    public bool EADesktop { get; set; } = false;

    [Option("ea", HelpText = "Search for EA app games", Hidden = true, Group = "Handlers")]
    public bool EA { get; set; } = false;

    // alias for "epic"
    [Option("egs", HelpText = "Search for Epic Games Launcher games", Hidden = true)]
    public bool EGS { get; set; } = false;

    [Option("epic", HelpText = "Search for Epic Games Launcher games", Hidden = true, Group = "Handlers")]
    public bool Epic { get; set; } = false;

    [Option("gamejolt", HelpText = "Search for Game Jolt Client games", Hidden = true, Group = "Handlers")]
    public bool GameJolt { get; set; } = false;

    [Option("gog", HelpText = "Search for GOG GALAXY games", Hidden = true, Group = "Handlers")]
    public bool GOG { get; set; } = false;

    [Option("heroic", HelpText = "Search for games from Heroic", Hidden = true, Group = "Handlers")]
    public bool Heroic { get; set; } = false;

    [Option("humble", HelpText = "Search for Humble App games", Hidden = true, Group = "Handlers")]
    public bool Humble { get; set; } = false;

    // alias for "ig"
    [Option("igclient", HelpText = "Search for Indiegala Client games", Hidden = true)]
    public bool Indiegala { get; set; } = false;

    // alias for "ig"
    [Option("igclient", HelpText = "Search for Indiegala Client games", Hidden = true)]
    public bool IGClient { get; set; } = false;

    [Option("ig", HelpText = "Search for Indiegala Client games", Hidden = true, Group = "Handlers")]
    public bool IG { get; set; } = false;

    [Option("itch", HelpText = "Search for itch games", Hidden = true, Group = "Handlers")]
    public bool Itch { get; set; } = false;

    [Option("legacy", HelpText = "Search for Legacy Games Launcher games", Hidden = true, Group = "Handlers")]
    public bool Legacy { get; set; } = false;

    [Option("mame", HelpText = "Search for MAME ROMs (path to .exe required)", MetaValue = "PATH", Group = "Handlers")]
    public string? MAME { get; set; }

    [Option("oculus", HelpText = "Search for Oculus games", Hidden = true, Group = "Handlers")]
    public bool Oculus { get; set; } = false;

    [Option("origin", HelpText = "Search for Origin games", Hidden = true, Group = "Handlers")]
    public bool Origin { get; set; } = false;

    [Option("paradox", HelpText = "Search for Paradox Launcher games", Hidden = true, Group = "Handlers")]
    public bool Paradox { get; set; } = false;

    [Option("plarium", HelpText = "Search for Plarium Play games", Hidden = true, Group = "Handlers")]
    public bool Plarium { get; set; } = false;

    [Option("riot", HelpText = "Search for Riot Client games", Hidden = true, Group = "Handlers")]
    public bool Riot { get; set; } = false;

    [Option("robotcache", HelpText = "Search for Robot Cache Client games", Hidden = true, Group = "Handlers")]
    public bool RobotCache { get; set; } = false;

    [Option("rockstar", HelpText = "Search for Rockstar Games Launcher games", Hidden = true, Group = "Handlers")]
    public bool Rockstar { get; set; } = false;

    [Option("steam", HelpText = "Search for Steam games", Hidden = true, Group = "Handlers")]
    public bool Steam { get; set; } = false;

    /*
    [Option("tgdb", HelpText = "Search for TheGamesDb.net games", Group = "Handlers")]
    public bool TheGamesDB { get; set; } = false;
    */

    // alias for "ubisoft"
    [Option("uplay", HelpText = "Search for Ubisoft Connect games", Hidden = true)]
    public bool Uplay { get; set; } = false;

    [Option("ubisoft", HelpText = "Search for Ubisoft Connect games", Hidden = true, Group = "Handlers")]
    public bool Ubisoft { get; set; } = false;

    // alias for "wargaming"
    [Option("wargamingnet", HelpText = "Search for Wargaming.Net Game Center games", Hidden = true)]
    public bool WargamingNet { get; set; } = false;

    [Option("wargaming", HelpText = "Search for Wargaming.Net Game Center games", Hidden = true, Group = "Handlers")]
    public bool Wargaming { get; set; } = false;

    [Option("winget", HelpText = "Search for Windows Package Manager packages", Hidden = true, Group = "Handlers")]
    public bool Winget { get; set; } = false;

    [Option("xbox", HelpText = "Search for Xbox Games Pass games", Hidden = true, Group = "Handlers")]
    public bool Xbox { get; set; } = false;

    [Option('s', "steamapi", HelpText = "Specify Steam API key from <https://steamcommunity.com/dev/apikey> (optional)", MetaValue = "KEY")]
    public string? SteamAPI { get; set; }

    /*
    [Option('t', "tgdbapi", HelpText = "Specify TheGamesDb.net API key from <https://api.thegamesdb.net/key.php> (optional)", MetaValue = "KEY")]
    public string? TheGamesDBAPI { get; set; }
    */

    [Option('w', "wine", HelpText = "Search for Wine prefixes")]
    public bool Wine { get; set; } = false;

    [Option('b', "bottles", HelpText = "Search for Wine prefixes managed with Bottles")]
    public bool Bottles { get; set; } = false;

    [Option('i', "installed", HelpText = "Only retrieve installed games")]
    public bool Installed { get; set; } = false;

    // alias for "parent"
    [Option("base", HelpText = "Only retrieve base games (no DLCs or clones)", Hidden = true)]
    public bool Base { get; set; } = false;

    [Option('p', "parent", HelpText = "Only retrieve base games (no DLCs or clones)")]
    public bool Parent { get; set; } = false;

    [Option('o', "owned", HelpText = "Retrieve only owned games")]
    public bool Owned { get; set; } = false;

    [Option('g', "games", HelpText = "Retrieve only games, not software")]
    public bool Games { get; set; } = false;
}
