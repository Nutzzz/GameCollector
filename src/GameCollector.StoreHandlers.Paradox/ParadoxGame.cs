using System;
using System.Collections.Generic;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Paradox;

/// <summary>
/// Represents a game installed with Paradox Launcher.
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="InstallationPath"></param>
/// <param name="GameDataPath"></param>
/// <param name="ExePath"></param>
/// <param name="ExeArgs"></param>
/// <param name="LastLaunch"></param>
/// <param name="AppIcon"></param>
/// <param name="AppTaskbarIcon"></param>
/// <param name="Background"></param>
/// <param name="Logo"></param>
[PublicAPI]
public record ParadoxGame(ParadoxGameId Id,
                      string Name,
                      AbsolutePath InstallationPath,
                      AbsolutePath GameDataPath,
                      AbsolutePath ExePath = new(),
                      string? ExeArgs = null,
                      AbsolutePath AppIcon = new(),
                      ulong? LastLaunch = null,
                      AbsolutePath AppTaskbarIcon = new(),
                      AbsolutePath Background = new(),
                      AbsolutePath Logo = new()) :
    GameData(Handler: Handler.StoreHandler_Paradox,
             GameId: Id.ToString(),
             GameName: Name,
             GamePath: InstallationPath,
             SavePath: GameDataPath,
             Launch: ExePath,
             LaunchArgs: ExeArgs ?? "",
             Icon: AppIcon,
             LastRunDate: LastLaunch is null ? null : DateTimeOffset.FromUnixTimeMilliseconds((long)LastLaunch).UtcDateTime,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["TaskbarIcon"] = [AppTaskbarIcon == default ? "" : AppTaskbarIcon.GetFullPath(),],
                 ["Background"] = [Background == default ? "" : Background.GetFullPath(),],
                 ["Logo"] = [Logo == default ? "" : Logo.GetFullPath(),],
             });
