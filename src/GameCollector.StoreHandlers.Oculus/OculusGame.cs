using System;
using System.Collections.Generic;
using System.Linq;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Oculus;

/// <summary>
/// Represents a game installed with Oculus.
/// </summary>
/// <param name="HashKey"></param>
/// <param name="DisplayName"></param>
/// <param name="InstallPath"></param>
/// <param name="LaunchFile"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsExpired"></param>
/// <param name="Description"></param>
/// <param name="Genres"></param>
/// <param name="CanonicalName"></param>
[PublicAPI]
public record OculusGame(OculusGameId HashKey,
                      string DisplayName,
                      AbsolutePath InstallPath = new(),
                      AbsolutePath LaunchFile = new(),
                      bool IsInstalled = true,
                      bool IsExpired = false,
                      string? Description = null,
                      IList<string>? Genres = null,
                      string? CanonicalName = null) :
    GameData(Handler: Handler.StoreHandler_Oculus,
             GameId: HashKey.ToString(),
             GameName: DisplayName,
             GamePath: InstallPath,
             Launch: LaunchFile,
             Icon: LaunchFile,
             IsInstalled: IsInstalled,
             Problems: IsExpired ? new List<Problem>() { Problem.ExpiredTrial } : null,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = [Description ?? "",],
                 ["Genres"] = Genres is null ? [] : [.. Genres],
                 ["CanonicalName"] = [CanonicalName ?? "",],
             });
