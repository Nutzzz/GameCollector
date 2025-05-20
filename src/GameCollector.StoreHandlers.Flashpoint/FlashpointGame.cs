using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameCollector.StoreHandlers.Flashpoint;

/// <summary>
/// Represents a game installed with Flashpoint launcher.
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="GamePath"></param>
/// <param name="ApplicationPath"></param>
/// <param name="LaunchCommand"></param>
/// <param name="LaunchUrl"></param>
/// <param name="Logo"></param>
/// <param name="Screenshot"></param>
/// <param name="DateModified"></param>
/// <param name="LastPlayed"></param>
/// <param name="PlayCounter"></param>
/// <param name="Playtime"></param>
/// <param name="IsInstalled"></param>
/// <param name="Problems"></param>
/// <param name="ReleaseDate"></param>
/// <param name="Version"></param>
/// <param name="Developer"></param>
/// <param name="Publisher"></param>
/// <param name="Series"></param>
/// <param name="OriginalDescription"></param>
/// <param name="Tags"></param>
/// <param name="Platforms"></param>
/// <param name="PlayModes"></param>
/// <param name="Notes"></param>
[PublicAPI]
public record FlashpointGame(FlashpointGameId Id,
                            string Title,
                            AbsolutePath GamePath,
                            AbsolutePath ApplicationPath = default,
                            string? LaunchCommand = "",
                            string? LaunchUrl = "",
                            AbsolutePath Logo = default,
                            DateTime? DateModified = null,
                            DateTime? LastPlayed = null,
                            uint? PlayCounter = null,
                            TimeSpan? Playtime = null,
                            bool IsInstalled = true,
                            IList<Problem>? Problems = null,
                            DateTime? ReleaseDate = null,
                            string? Version = null,
                            string? Developer = null,
                            string? Publisher = null,
                            string? Series = null,
                            string? OriginalDescription = null,
                            string? Screenshot = null,
                            IList<string>? Tags = null,
                            IList<string>? Platforms = null,
                            IList<string>? PlayModes = null,
                            string? Notes = null) :
    GameData(Handler: Handler.StoreHandler_Flashpoint,
             GameId: Id.ToString(),
             GameName: Title,
             GamePath: GamePath,
             Launch: ApplicationPath,
             LaunchArgs: LaunchCommand ?? "",
             LaunchUrl: LaunchUrl ?? "",
             Icon: Logo,
             UpdateDate: DateModified,
             LastRunDate: LastPlayed,
             NumRuns: PlayCounter ?? 0,
             RunTime: Playtime,
             IsInstalled: IsInstalled,
             Problems: Problems,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["ReleaseDate"] = [ReleaseDate?.ToString(CultureInfo.InvariantCulture) ?? "",],
                 ["DefaultVersion"] = [Version ?? "",],
                 ["Developer"] = [Developer ?? "",],
                 ["Publisher"] = [Publisher ?? "",],
                 ["Series"] = [Series ?? "",],
                 ["Description"] = [OriginalDescription ?? "",],
                 ["Screenshot"] = [Screenshot ?? ""],
                 ["Genres"] = Tags?.ToList() ?? [],
                 ["Platforms"] = Platforms?.ToList() ?? [],
                 ["Players"] = [PlayModeToNumPlayers(PlayModes?.ToList()).ToString(CultureInfo.InvariantCulture) ?? "",],
                 ["Notes"] = [Notes ?? "",],
             })
{
    internal static int PlayModeToNumPlayers(IList<string>? mode)
    {
        if (mode is null)
            return 0;

        if (mode.Contains("Multiplayer", StringComparer.Ordinal) || mode.Contains("Cooperative", StringComparer.Ordinal))
            return 2;
        if (mode.Contains("Single Player", StringComparer.Ordinal))
            return 1;

        return 0;
    }
}
