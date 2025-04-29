using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using GameCollector.DataHandlers.TheGamesDB;
using GameCollector.DataHandlers.TheGamesDB.Properties;
using GameFinder.Common;
using JetBrains.Annotations;
using TheGamesDBApiWrapper.Data.ApiClasses;

namespace GameCollector.DataHandlers.TheGamesDB;

/// <summary>
/// Represents game metadata from TheGamesDB.net.
/// </summary>
/// <param name="Id"></param>
/// <param name="GameTitle"></param>
/// <param name="ReleaseDate"></param>
/// <param name="PlatformId"></param>
/// <param name="PlatformName"></param>
/// <param name="RegionId"></param>
/// <param name="CountryId"></param>
/// <param name="Overview"></param>
/// <param name="Youtube"></param>
/// <param name="Players"></param>
/// <param name="Coop"></param>
/// <param name="Rating"></param>
/// <param name="Developers"></param>
/// <param name="Genres"></param>
/// <param name="Publishers"></param>
/// <param name="Alternates"></param>
/// <param name="BannerUrl"></param>
/// <param name="BoxartUrl"></param>
/// <param name="BoxartBackUrl"></param>
/// <param name="ClearlogoUrl"></param>
/// <param name="FanartUrl"></param>
/// <param name="ScreenshotUrl"></param>
/// <param name="TitlescreenUrl"></param>
/// <param name="HDD"></param>
/// <param name="OS"></param>
/// <param name="Processor"></param>
/// <param name="RAM"></param>
/// <param name="Sound"></param>
/// <param name="Video"></param>
[PublicAPI]
public record TheGamesDBGame(TheGamesDBGameId Id,
                             string? GameTitle = null,
                             IList<string>? Alternates = null,
                             DateTime? ReleaseDate = null,
                             int? PlatformId = null,
                             string? PlatformName = null,
                             ushort? RegionId = null,
                             ushort? CountryId = null,
                             string? Overview = null,
                             string? Youtube = null,
                             ushort? Players = null,
                             string? Coop = null,
                             string? Rating = null,
                             IList<string>? Developers = null,
                             IList<string>? Genres = null,
                             IList<string>? Publishers = null,
                             string? BannerUrl = null,
                             string? BoxartUrl = null,
                             string? BoxartBackUrl = null,
                             string? ClearlogoUrl = null,
                             string? FanartUrl = null,
                             string? ScreenshotUrl = null,
                             string? TitlescreenUrl = null,
                             string? HDD = null,
                             string? OS = null,
                             string? Processor = null,
                             string? RAM = null,
                             string? Sound = null,
                             string? Video = null) :
    GameData(Handler: Handler.DataHandler_TheGamesDB,
             GameId: Id.ToString(),
             GameName: GameTitle ?? "",
             GamePath: new(),
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["AlternateNames"] = Alternates?.ToList() ?? [],
                 ["ReleaseDate"] = [ReleaseDate is null ? "" : ((DateTime)ReleaseDate).ToString(CultureInfo.InvariantCulture),],
                 ["Description"] = [Overview ?? "",],
                 ["BannerUrl"] = [BannerUrl ?? "",],
                 ["BoxartUrl"] = [BoxartUrl ?? "",],
                 ["BoxartBackUrl"] = [BoxartBackUrl ?? "",],
                 ["ClearlogoUrl"] = [ClearlogoUrl ?? "",],
                 ["FanartUrl"] = [FanartUrl ?? "",],
                 ["ScreenshotUrl"] = [ScreenshotUrl ?? "",],
                 ["TitlescreenUrl"] = [TitlescreenUrl ?? "",],
                 ["Developers"] = Developers?.ToList() ?? [],
                 ["Publishers"] = Publishers?.ToList() ?? [],
                 ["AgeRating"] = [Rating ?? ""],
                 ["Players"] = [Players?.ToString() ?? ""],
                 ["Cooperative"] = [Coop is null ? "" : (Coop.Equals("Yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "")],
                 ["Genres"] = Genres?.ToList() ?? [],
                 ["Platform"] = [PlatformName ?? ""],
                 ["PlatformId"] = [PlatformId?.ToString(CultureInfo.InvariantCulture) ?? ""],
                 ["HDD"] = [HDD ?? "",],
                 ["OS"] = [OS ?? "",],
                 ["Processor"] = [Processor ?? "",],
                 ["RAM"] = [RAM ?? "",],
                 ["Sound"] = [Sound ?? "",],
                 ["Video"] = [Video ?? "",],
             });
