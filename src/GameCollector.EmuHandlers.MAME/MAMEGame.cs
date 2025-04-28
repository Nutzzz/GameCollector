using System;
using System.Collections.Generic;
using System.Linq;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.EmuHandlers.MAME;

/// <summary>
/// Represents a ROM for MAME Emulator.
/// </summary>
/// <param name="Name"></param>
/// <param name="Description"></param>
/// <param name="Path"></param>
/// <param name="MAMEExecutable"></param>
/// <param name="CommandLineArgs"></param>
/// <param name="Icon"></param>
/// <param name="IsAvailable"></param>
/// <param name="Problems"></param>
/// <param name="Parent"></param>
/// <param name="Year"></param>
/// <param name="Manufacturer"></param>
/// <param name="Categories"></param>
/// <param name="IsMature"></param>
/// <param name="Players"></param>
/// <param name="DriverStatus"></param>
/// <param name="DisplayType"></param>
/// <param name="DisplayRotation"></param>
/// <param name="VersionAdded"></param>
[PublicAPI]
public record MAMEGame(MAMEGameId Name,
                       string Description,
                       AbsolutePath Path,
                       AbsolutePath MAMEExecutable = new(),
                       string CommandLineArgs = "",
                       AbsolutePath Icon = new(),
                       bool IsAvailable = true,
                       IList<Problem>? Problems = null,
                       string? Parent = null,
                       string? Year = null,
                       string? Manufacturer = null,
                       IList<string>? Categories = null,
                       bool IsMature = false,
                       string? Players = null,
                       string? DriverStatus = null,
                       string? DisplayType = null,
                       string? DisplayRotation = null,
                       string? VersionAdded = null) :
    GameData(Handler: Handler.EmuHandler_MAME,
             GameId: Name.ToString() ?? "",
             GameName: Description,
             GamePath: Path,
             Launch: MAMEExecutable,
             LaunchArgs: (Name.ToString() ?? "") + (string.IsNullOrEmpty(CommandLineArgs) ? "" : " " + CommandLineArgs),
             Icon: Icon,
             IsInstalled: IsAvailable,
             Problems: Problems,
             BaseGame: Parent,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["ReleaseDate"] = [Year ?? "",],
                 ["Manufacturer"] = [Manufacturer ?? "",],
                 ["Genres"] = Categories?.ToList() ?? [],
                 ["AgeRating"] = [IsMature ? "adults_only" : "",],
                 ["Players"] = [Players ?? "",],
                 ["DriverStatus"] = [DriverStatus ?? "",],
                 ["DisplayType"] = [DisplayType ?? "",],
                 ["DisplayRotation"] = [DisplayRotation ?? "",],
                 ["VersionAdded"] = [VersionAdded ?? "",],
             });
