using System;
using System.Collections.Generic;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Rockstar;

/// <summary>
/// Represents a game installed with Rockstar Games Launcher.
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="InstallFolder"></param>
/// <param name="Launch"></param>
/// <param name="Uninstall"></param>
/// <param name="UninstallArgs"></param>
/// <param name="Publisher"></param>
/// <param name="UrlInfoAbout"></param>
/// <param name="HelpLink"></param>
[PublicAPI]
public record RockstarGame(RockstarGameId Id,
                      string Name,
                      AbsolutePath InstallFolder,
                      AbsolutePath Launch = new(),
                      AbsolutePath Uninstall = new(),
                      string UninstallArgs = "",
                      string Publisher = "",
                      string UrlInfoAbout = "",
                      string HelpLink = "") :
    GameData(Handler: Handler.StoreHandler_Rockstar,
             GameId: Id.ToString(),
             GameName: Name,
             GamePath: InstallFolder,
             Launch: Launch,
             Icon: Launch,
             Uninstall: Uninstall,
             UninstallArgs: UninstallArgs,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Publisher"] = [Publisher],
                 ["WebInfo"] = [UrlInfoAbout],
                 ["WebSupport"] = [HelpLink],
             });
