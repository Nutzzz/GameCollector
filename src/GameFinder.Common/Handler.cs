using System.ComponentModel;

namespace GameFinder.Common;

/// <summary>
/// Handler identification
/// </summary>
public enum Handler
{
    /// <summary>
    /// Bethesda.net Launcher store handler (deprecated)
    /// </summary>
    [Description("GameCollector.StoreHandlers.BethNet")]
    StoreHandler_BethNet,
    /// <summary>
    /// EA app store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.EADesktop")]
    StoreHandler_EADesktop,
    /// <summary>
    /// Epic Games Launcher store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.EGS")]
    StoreHandler_EGS,
    /// <summary>
    /// GOG GALAXY store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.GOG")]
    StoreHandler_GOG,
    /// <summary>
    /// Origin store handler
    /// </summary>
    /// <remarks>Suggest transitioning to GameCollector.StoreHandlers.EADesktop</remarks>
    [Description("GameCollector.StoreHandlers.Origin")]
    StoreHandler_Origin,
    /// <summary>
    /// Steam store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Steam")]
    StoreHandler_Steam,
    /// <summary>
    /// Microsoft Xbox store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Xbox")]
    StoreHandler_Xbox,
    /// <summary>
    /// Amazon Games store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Amazon")]
    StoreHandler_Amazon,
    /// <summary>
    /// Arc store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Arc")]
    StoreHandler_Arc,
    /// <summary>
    /// Blizzard Battle.net store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.BattleNet")]
    StoreHandler_BattleNet,
    /// <summary>
    /// Big Fish Games store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.BigFish")]
    StoreHandler_BigFish,
    /// <summary>
    /// Game Jolt Client store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.GameJolt")]
    StoreHandler_GameJolt,
    /// <summary>
    /// Humble App store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Humble")]
    StoreHandler_Humble,
    /// <summary>
    /// Indiegala Client store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.IGClient")]
    StoreHandler_IGClient,
    /// <summary>
    /// itch store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Itch")]
    StoreHandler_Itch,
    /// <summary>
    /// Legacy Games Launcher store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Legacy")]
    StoreHandler_Legacy,
    /// <summary>
    /// Oculus store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Oculus")]
    StoreHandler_Oculus,
    /// <summary>
    /// Paradox Launcher store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Paradox")]
    StoreHandler_Paradox,
    /// <summary>
    /// Plarium Play store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Plarium")]
    StoreHandler_Plarium,
    /// <summary>
    /// Riot Client store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Riot")]
    StoreHandler_Riot,
    /// <summary>
    /// RobotCache store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.RobotCache")]
    StoreHandler_RobotCache,
    /// <summary>
    /// Rockstar Games Launcher store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Rockstar")]
    StoreHandler_Rockstar,
    /// <summary>
    /// Ubisoft Connect store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.Ubisoft")]
    StoreHandler_Ubisoft,
    /// <summary>
    /// Wargaming.net Game Center store handler
    /// </summary>
    [Description("GameCollector.StoreHandlers.WargamingNet")]
    StoreHandler_WargamingNet,
    /// <summary>
    /// Heroic launcher handler
    /// </summary>
    [Description("GameCollector.Launcher.Heroic")]
    Launcher_Heroic,
    /// <summary>
    /// Dolphin Emulator emulation handler
    /// </summary>
    [Description("GameCollector.EmuHandlers.Dolphin")]
    EmuHandler_Dolphin,
    /// <summary>
    /// Multiple Arcade Machines Emulator emulation handler
    /// </summary>
    [Description("GameCollector.EmuHandlers.MAME")]
    EmuHandler_MAME,
    /// <summary>
    /// Windows Package Manager package handler
    /// </summary>
    [Description("GameCollector.PkgHandlers.Winget")]
    PkgHandler_Winget,
    /// <summary>
    /// TheGamesDb.net data handler
    /// </summary>
    [Description("GameCollector.DataHandlers.TheGamesDb")]
    DataHandler_TheGamesDb,
}
