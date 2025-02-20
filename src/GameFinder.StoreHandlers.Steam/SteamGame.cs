using System;
using System.Collections.Generic;
using FluentResults;
using GameFinder.Common;
using GameCollector.StoreHandlers.Steam.Models;
using GameCollector.StoreHandlers.Steam.Models.ValueTypes;
using GameCollector.StoreHandlers.Steam.Services;
using JetBrains.Annotations;
using NexusMods.Paths;
using Steam.Models.SteamCommunity;

namespace GameCollector.StoreHandlers.Steam;

/// <summary>
/// Represents a game installed with Steam.
/// </summary>
/// <param name="SteamPath">Gets the path to the global Steam installation.</param>
/// <param name="AppManifest">Gets the parsed <see cref="AppManifest"/> of this game.</param>
/// <param name="RegistryEntry">Gets the parsed <see cref="RegistryEntry"/> of this game.</param>
/// <param name="LibraryFolder">Gets the library folder that contains this game.</param>
/// <param name="OwnedGame"></param>
/// <param name="IsInstalled"></param>
[PublicAPI]
public sealed record SteamGame(AbsolutePath SteamPath,
                        AppManifest? AppManifest,
                        RegistryEntry? RegistryEntry,
                        LibraryFolder? LibraryFolder,
                        OwnedGameModel? OwnedGame,
                        bool IsInstalled) :
    GameData(Handler: Handler.StoreHandler_Steam,
             GameId: AppManifest is not null ? AppManifest.AppId.ToString() :
                (OwnedGame is not null ? OwnedGame.AppId.ToString() : ""),
             GameName: AppManifest is not null ? AppManifest.Name :
                (OwnedGame is not null ? OwnedGame.Name : ""),
             GamePath: AppManifest is not null ? AppManifest.InstallationDirectory : new(),
             SavePath: (AppManifest is not null) ? AppManifest.GetUserDataDirectoryPath(SteamPath) : new(),
             LaunchUrl: AppManifest is not null ? RunGameProtocol + AppManifest.AppId.ToString() :
                (OwnedGame is not null ? RunGameProtocol + OwnedGame.AppId.ToString() : ""),
             Icon: RegistryEntry?.DisplayIcon ?? new(),
             UninstallUrl: AppManifest is not null ? UninstProtocol + AppManifest.AppId.ToString() : "",
             RunTime: OwnedGame?.PlaytimeForever,
             IsInstalled: IsInstalled,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["LibraryFolder"] = LibraryFolder is not null ? new() { LibraryFolder.Path.GetFullPath() } : new(),
                 ["Publisher"] = RegistryEntry is not null ? new() { RegistryEntry.Publisher } : new(),
                 ["WebInfo"] = RegistryEntry is not null ? new() { RegistryEntry.URLInfoAbout } : new(),
                 ["IconUrl"] = (OwnedGame is not null && !string.IsNullOrEmpty(OwnedGame.ImgIconUrl)) ? new() { $"{SteamMediaUrl}{OwnedGame.AppId}/{OwnedGame.ImgIconUrl}.jpg" } : new(),
                 ["ImageUrl"] = AppManifest is not null ? new() { $"{SteamStaticUrl}{AppManifest.AppId}/library_600x900.jpg" } :
                    (OwnedGame is not null ? new() { $"{SteamStaticUrl}{OwnedGame.AppId}/library_600x900.jpg" } : new()),
                 ["ImageWideUrl"] = AppManifest is not null ? new() { $"{SteamStaticUrl}{AppManifest.AppId}/header.jpg" } :
                    (OwnedGame is not null ? new() { $"{SteamStaticUrl}{OwnedGame.AppId}/header.jpg" } : new()),
             })
{
    internal const string RunGameProtocol = "steam://rungameid/";
    internal const string UninstProtocol = "steam://uninstall/";
    internal const string SteamStaticUrl = "https://cdn.akamai.steamstatic.com/steam/apps/";
    internal const string SteamMediaUrl = "http://media.steampowered.com/steamcommunity/public/images/apps/";

    #region Helpers

    /// <inheritdoc cref="Models.AppManifest.AppId"/>
    public AppId AppId => AppManifest is not null ? AppManifest.AppId :
        (OwnedGame is not null ? (AppId)OwnedGame.AppId : (AppId)0);

    /// <inheritdoc cref="Models.AppManifest.Name"/>
    public string Name => AppManifest is not null ? AppManifest.Name :
        (OwnedGame is not null ? OwnedGame.Name : "");

    /// <summary>
    /// Gets the absolute path to the game's installation directory.
    /// </summary>
    public AbsolutePath? Path => AppManifest?.InstallationDirectory;

    /// <summary>
    /// Gets the absolute path to the cloud saves directory.
    /// </summary>
    public AbsolutePath GetCloudSavesDirectoryPath() => AppManifest is not null ? AppManifest.GetUserDataDirectoryPath(SteamPath) : new();

#if !WIN64
    /// <summary>
    /// Gets the Wine prefix managed by Proton for this game, if it exists.
    /// </summary>
    public ProtonWinePrefix? GetProtonPrefix()
    {
        var protonDirectory = AppManifest is not null ? AppManifest.GetCompatabilityDataDirectoryPath() : new();
        if (!protonDirectory.DirectoryExists()) return null;

        var configurationDirectory = protonDirectory.Combine("pfx");
        return new ProtonWinePrefix
        {
            ConfigurationDirectory = configurationDirectory,
            ProtonDirectory = protonDirectory,
        };
    }
#endif

    /// <summary>
    /// Uses <see cref="WorkshopManifestParser"/> to parse the workshop manifest
    /// file at <see cref="Models.AppManifest.GetWorkshopManifestFilePath"/>.
    /// </summary>
    /// <seealso cref="WorkshopManifestParser"/>
    [Pure]
    [System.Diagnostics.Contracts.Pure]
    [MustUseReturnValue]
    public Result<WorkshopManifest> ParseWorkshopManifest()
    {
        var workshopManifestFilePath = AppManifest is not null ? AppManifest.GetWorkshopManifestFilePath() : new();
        var result = WorkshopManifestParser.ParseManifestFile(workshopManifestFilePath);
        return result;
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public bool Equals(SteamGame? other) => AppManifest is not null ? AppManifest.Equals(other?.AppManifest) :
        (OwnedGame is not null && OwnedGame.Equals(other?.OwnedGame));

    /// <inheritdoc/>
    public override int GetHashCode() => AppManifest is not null ? AppManifest.GetHashCode() :
        (OwnedGame is not null ? OwnedGame.GetHashCode() : -1);

    #endregion
}

