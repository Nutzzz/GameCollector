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
                       string? Description = "",
                       string? Publisher = "",
                       Genre? Genre = (Genre)(-1),
                       string? CoverArtUrl = "") :
    GameData(GameId: InstallerUuid.ToString(),
             GameName: ProductName,
             GamePath: InstDir,
             Launch: ExePath,
             Icon: DisplayIcon == default ? ExePath : DisplayIcon,
             Uninstall: UninstallString,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = new() { Description ?? "", },
                 ["Publishers"] = new() { Publisher ?? "", },
                 ["Genres"] = new() { Genre == (Genre)(-1) ? "" : Genre.ToString() ?? "", },
                 ["ImageUrl"] = new() { CoverArtUrl ?? "", },
             });
