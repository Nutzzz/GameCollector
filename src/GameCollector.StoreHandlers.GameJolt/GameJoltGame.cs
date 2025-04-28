using System;
using System.Collections.Generic;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.GameJolt;

/// <summary>
/// Represents a game installed with Game Jolt Client.
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="InstallDir"></param>
/// <param name="ExecutablePath"></param>
/// <param name="Developer"></param>
/// <param name="ImageUrl"></param>
/// <param name="HeaderUrl"></param>
/// <param name="PackageId"></param>
[PublicAPI]
public record GameJoltGame(GameJoltGameId Id,
                      string Title,
                      AbsolutePath InstallDir,
                      AbsolutePath ExecutablePath = new(),
                      string? Developer = null,
                      string? ImageUrl = null,
                      string? HeaderUrl = null,
                      ulong? PackageId = null) :
    GameData(Handler: Handler.StoreHandler_GameJolt,
             GameId: Id.ToString(),
             GameName: Title,
             GamePath: InstallDir,
             Launch: ExecutablePath,
             Icon: ExecutablePath,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Developers"] = [Developer ?? "",],
                 ["ImageUrl"] = [ImageUrl ?? "",],
                 ["ImageWideUrl"] = [HeaderUrl ?? "",],
                 ["PackageId"] = [PackageId.ToString() ?? "",],
             });
