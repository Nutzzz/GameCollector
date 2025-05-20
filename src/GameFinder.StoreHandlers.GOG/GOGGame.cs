using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameCollector.StoreHandlers.GOG;

/// <summary>
/// Represents a game installed with GOG Galaxy.
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="Path"></param>
/// <param name="BuildId"></param>
/// <param name="Launch"></param>
/// <param name="LaunchParam"></param>
/// <param name="LaunchUrl"></param>
/// <param name="Exe"></param>
/// <param name="UninstallCommand"></param>
/// <param name="InstallDate"></param>
/// <param name="LastPlayedDate"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsOwned"></param>
/// <param name="IsHidden"></param>
/// <param name="Tags"></param>
/// <param name="MyRating"></param>
/// <param name="ParentId"></param>
/// <param name="ReleaseDate"></param>
/// <param name="StoreUrl"></param>
/// <param name="SupportUrl"></param>
/// <param name="BoxArtUrl"></param>
/// <param name="LogoUrl"></param>
/// <param name="IconUrl"></param>
[PublicAPI]
public record GOGGame(GOGGameId Id,
                      string Name,
                      AbsolutePath Path,
                      ulong BuildId,
                      AbsolutePath Launch = new(),
                      string LaunchParam = "",
                      string LaunchUrl = "",
                      AbsolutePath Exe = new(),
                      AbsolutePath UninstallCommand = new(),
                      DateTime? InstallDate = null,
                      DateTime? LastPlayedDate = null,
                      bool IsInstalled = true,
                      bool IsOwned = true,
                      bool IsHidden = false,
                      IList<string>? Tags = null,
                      ushort? MyRating = null,
                      GOGGameId? ParentId = default,
                      DateTime? ReleaseDate = null,
                      string StoreUrl = "",
                      string SupportUrl = "",
                      string BoxArtUrl = "",
                      string LogoUrl = "",
                      string IconUrl = "") :
    GameData(Handler: Handler.StoreHandler_GOG,
             GameId: Id.ToString() ?? "",
             GameName: Name,
             GamePath: Path,
             Launch: Launch,
             LaunchArgs: LaunchParam,
             LaunchUrl: LaunchUrl,
             Icon: Exe,
             Uninstall: UninstallCommand,
             InstallDate: InstallDate,
             LastRunDate: LastPlayedDate,
             IsInstalled: IsInstalled,
             IsOwned: IsOwned,
             IsHidden: IsHidden,
             Tags: Tags,
             MyRating: MyRating,
             BaseGame: ParentId.ToString(),
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["ReleaseDate"] = [ReleaseDate is null ? "" : ((DateTime)ReleaseDate).ToString(CultureInfo.InvariantCulture)],
                 ["WebInfo"] = [StoreUrl],
                 ["WebSupport"] = [SupportUrl],
                 ["ImageUrl"] = [BoxArtUrl],
                 ["ImageWideUrl"] = [LogoUrl],
                 ["IconUrl"] = [IconUrl],
                 ["BuildId"] = [BuildId.ToString()],
             });
