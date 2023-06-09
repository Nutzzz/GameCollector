using System;
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
    GameData(GameId: Id.ToString(),
             Name: Title,
             Path: InstallDir,
             Launch: ExecutablePath,
             Icon: ExecutablePath,
             Metadata: new(StringComparer.OrdinalIgnoreCase)
             {
                 ["Developers"] = new() { Developer ?? "", },
                 ["ImageUrl"] = new() { ImageUrl ?? "", },
                 ["ImageWideUrl"] = new() { HeaderUrl ?? "", },
                 ["PackageId"] = new() { PackageId.ToString() ?? "", },
             });
