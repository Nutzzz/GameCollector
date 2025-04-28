using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.IGClient;

/// <summary>
/// Represents a game installed with Indiegala IGClient.
/// </summary>
/// <param name="IdKeyName"></param>
/// <param name="ItemName"></param>
/// <param name="Path"></param>
/// <param name="ExePath"></param>
/// <param name="ExeArgs"></param>
/// <param name="IsInstalled"></param>
/// <param name="DescriptionShort"></param>
/// <param name="DescriptionLong"></param>
/// <param name="DevImage"></param>
/// <param name="DevCover"></param>
/// <param name="SluggedName"></param>
/// <param name="Specs"></param>
/// <param name="Categories"></param>
/// <param name="Tags"></param>
/// <param name="AvgRating"></param>
[PublicAPI]
public record IGClientGame(IGClientGameId IdKeyName,
                         string ItemName,
                         AbsolutePath Path,
                         AbsolutePath ExePath = new(),
                         string ExeArgs = "",
                         bool IsInstalled = true,
                         string? DescriptionShort = null,
                         string? DescriptionLong = null,
                         string? DevImage = null,
                         string? DevCover = null,
                         string? SluggedName = null,
                         IList<string>? Specs = null,
                         IList<string>? Categories = null,
                         IList<string>? Tags = null,
                         decimal AvgRating = 0m) :
    GameData(Handler: Handler.StoreHandler_IGClient,
             GameId: IdKeyName.ToString(),
             GameName: ItemName,
             GamePath: Path,
             Launch: ExePath,
             LaunchArgs: ExeArgs,
             Icon: ExePath,
             IsInstalled: IsInstalled,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = [DescriptionShort ?? "",],
                 ["DescriptionLong"] = [DescriptionLong ?? "",],
                 ["ImageUrl"] = [DevImage ?? "",],
                 ["ImageWideUrl"] = [DevCover ?? "",],
                 ["SluggedName"] = [SluggedName ?? "",],
                 ["Players"] = [SpecsToNumPlayers(Specs?.ToList()).ToString(CultureInfo.InvariantCulture) ?? "",],
                 ["Genres"] = Categories?.ToList() ?? [],
                 ["Tags"] = Tags?.ToList() ?? [],
                 ["Rating"] = [AvgRating.ToString(CultureInfo.InvariantCulture) ?? "",],
             })
{
    internal static int SpecsToNumPlayers(List<string>? specs)
    {
        if (specs is null)
            return 0;

        if (specs.Contains("Multi-player", StringComparer.Ordinal) ||
                specs.Contains("Multiplayer", StringComparer.Ordinal))
            return 3;
        if (specs.Contains("Co-op", StringComparer.Ordinal) ||
            specs.Contains("Shared/Split Screen", StringComparer.Ordinal))
            return 2;
        if (specs.Contains("Single-player", StringComparer.Ordinal))
            return 1;

        return 0;
    }
}
