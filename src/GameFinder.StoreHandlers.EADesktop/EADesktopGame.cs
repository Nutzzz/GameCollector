using System;
using System.Collections.Generic;
using System.Linq;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.EADesktop;

/// <summary>
/// Represents a game installed with the EA Desktop app.
/// </summary>
/// <param name="EADesktopGameId">Id of the game.</param>
/// <param name="Name"></param>
/// <param name="BaseInstallPath">Absolute path to the game folder.</param>
/// <param name="Executable"></param>
/// <param name="UninstallCommand"></param>
/// <param name="UninstallParameters"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsDLC"></param>
/// <param name="Publisher"></param>
/// <param name="WebInfo"></param>
/// <param name="WebSupport"></param>
/// <param name="BaseSlug">Slug name of the game.</param>
/// <param name="ContentIDs"></param>
[PublicAPI]
public record EADesktopGame(EADesktopGameId EADesktopGameId,
                            string Name,
                            AbsolutePath BaseInstallPath,
                            AbsolutePath Executable = new(),
                            AbsolutePath UninstallCommand = new(),
                            string UninstallParameters = "",
                            bool IsInstalled = true,
                            bool IsDLC = false,
                            string Publisher = "",
                            string WebInfo = "",
                            string WebSupport = "",
                            string BaseSlug = "",
                            IList<string>? ContentIDs = null) :
    GameData(Handler: Handler.StoreHandler_EADesktop,
             GameId: EADesktopGameId.ToString() ?? "",
             GameName: Name,
             GamePath: BaseInstallPath,
             Launch: Executable,
             LaunchUrl: ContentIDs?.Count > 0 ? $"origin://game/launch?offerIds={ContentIDs[0]}" : "",
             Icon: Executable,
             Uninstall: UninstallCommand,
             UninstallArgs: UninstallParameters,
             IsInstalled: IsInstalled,
             BaseGame: IsDLC ? (!IsDLC).ToString() : null,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Publishers"] = [Publisher ?? "",],
                 ["WebInfo"] = [WebInfo ?? ""],
                 ["WebSupport"] = [WebSupport ?? "",],
                 ["BaseSlug"] = [BaseSlug],
                 ["ContentIDs"] = ContentIDs?.ToList() ?? [],
             })
{
    /// <summary>
    /// Returns the absolute path to the <c>installerdata.xml</c> file inside the <c>__Installer</c> folder
    /// of the game folder.
    /// </summary>
    /// <returns></returns>
    public AbsolutePath GetInstallerDataFile()
    {
        return BaseInstallPath
            .Combine("__Installer")
            .Combine("installerdata.xml");
    }
}
