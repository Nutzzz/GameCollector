using System;
using System.Collections;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Plarium;

/// <summary>
/// Represents a game installed with Plarium Play.
/// </summary>
/// <param name="ProductId"></param>
/// <param name="ProductName"></param>
/// <param name="InstallationPath"></param>
/// <param name="Launch"></param>
/// <param name="LaunchArgs"></param>
/// <param name="GameId"></param>
/// <param name="GameName"></param>
/// <param name="CompanyName"></param>
[PublicAPI]
public record PlariumGame(PlariumGameId ProductId,
                      string ProductName,
                      AbsolutePath InstallationPath,
                      AbsolutePath Launch = new(),
                      string LaunchArgs = "",
                      string? GameId = null,
                      string? GameName = null,
                      string? CompanyName = null) :
    GameData(GameId: ProductId.ToString(),
             GameName: ProductName,
             GamePath: InstallationPath,
             Launch: Launch,
             LaunchArgs: LaunchArgs,
             Metadata: new(StringComparer.OrdinalIgnoreCase)
             {
                 ["GameId"] = new() { GameId ?? "", },
                 ["ShortName"] = new() { GameName ?? "", },
                 ["Developers"] = new() { CompanyName ?? "", },
                 ["IconUrl"] = new() { string.IsNullOrEmpty(GameName) ? "" : $"https://cdn01.x-plarium.com/browser/content/plarium-play/games/notification_img/{GameName}.webp" },
             });
