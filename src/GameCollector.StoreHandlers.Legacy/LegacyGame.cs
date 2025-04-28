using System;
using System.Collections.Generic;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Legacy;

/// <summary>
/// Represents a game installed with Legacy Games Launcher.
/// </summary>
/// <param name="InstallerUuid"></param>
/// <param name="ProductName"></param>
/// <param name="InstDir"></param>
/// <param name="ExePath"></param>
/// <param name="DisplayIcon"></param>
/// <param name="UninstallString"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsOwned"></param>
/// <param name="NotFoundInData"></param>
/// <param name="Description"></param>
/// <param name="Publisher"></param>
/// <param name="Genre"></param>
/// <param name="CoverArtUrl"></param>
[PublicAPI]
public record LegacyGame(LegacyGameId InstallerUuid,
                       string ProductName,
                       AbsolutePath InstDir,
                       AbsolutePath ExePath = new(),
                       AbsolutePath DisplayIcon = new(),
                       AbsolutePath UninstallString = new(),
                       bool IsInstalled = false,
                       bool IsOwned = false,
                       bool NotFoundInData = false,
                       string? Description = "",
                       string? Publisher = "",
                       Genre? Genre = Genre.Unknown,
                       string? CoverArtUrl = "") :
    GameData(Handler: Handler.StoreHandler_Legacy,
             GameId: InstallerUuid.ToString(),
             GameName: ProductName,
             GamePath: InstDir,
             Launch: ExePath,
             Icon: DisplayIcon == default ? ExePath : DisplayIcon,
             Uninstall: UninstallString,
             IsInstalled: IsInstalled,
             IsOwned: IsOwned,
             Problems: NotFoundInData ? new List<Problem> { Problem.NotFoundInData } : null,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = [Description ?? "",],
                 ["Publishers"] = [Publisher ?? "",],
                 ["Genres"] = [Genre == Legacy.Genre.Unknown ? "" : Genre.ToString() ?? "",],
                 ["ImageUrl"] = [CoverArtUrl ?? "",],
             });
