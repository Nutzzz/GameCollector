using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using System;
using System.Collections.Generic;

namespace GameCollector.StoreHandlers.BattleNet;

/// <summary>
/// Represents a game installed with Blizzard Battle.net.
/// </summary>
/// <param name="ProductId"></param>
/// <param name="DirName"></param>
/// <param name="InstallPath"></param>
/// <param name="BinaryPath"></param>
/// <param name="Uninstaller"></param>
/// <param name="UninstallArgs"></param>
/// <param name="LastPlayed"></param>
/// <param name="AppDescription"></param>
[PublicAPI]
public record BattleNetGame(BattleNetGameId ProductId,
                      string DirName,
                      AbsolutePath InstallPath,
                      AbsolutePath BinaryPath = new(),
                      AbsolutePath Uninstaller = new(),
                      string UninstallArgs = "",
                      DateTime? LastPlayed = null,
                      string? AppDescription = "") :
    GameData(Handler: Handler.StoreHandler_BattleNet,
             GameId: ProductId.ToString(),
             GameName: DirName,
             GamePath: InstallPath,
             Launch: BinaryPath,
             Icon: BinaryPath,
             Uninstall: Uninstaller,
             UninstallArgs: UninstallArgs,
             LastRunDate: LastPlayed,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = [AppDescription ?? "",],
             });
