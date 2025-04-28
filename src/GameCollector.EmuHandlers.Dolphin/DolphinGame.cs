using System;
using System.Collections.Generic;
using System.Globalization;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.EmuHandlers.Dolphin;

/// <summary>
/// Represents a ROM for Dolphin Emulator.
/// </summary>
/// <param name="DolphinGameId"></param>
/// <param name="Title"></param>
/// <param name="Path"></param>
/// <param name="DolphinExecutable"></param>
/// <param name="ROMFile"></param>
/// <param name="Cover"></param>
/// <param name="AppLoaderDate"></param>
/// <param name="Publisher"></param>
/// <param name="System"></param>
/// <param name="Region"></param>
[PublicAPI]
public record DolphinGame(DolphinGameId DolphinGameId,
                         string Title,
                         AbsolutePath Path,
                         AbsolutePath DolphinExecutable = new(),
                         string ROMFile = "",
                         AbsolutePath Cover = new(),
                         DateTime? AppLoaderDate = null,
                         string? Publisher = "",
                         DolphinSystem? System = (DolphinSystem)(-1),
                         DolphinRegion? Region = (DolphinRegion)(-1)) :
    GameData(Handler: Handler.EmuHandler_Dolphin,
             GameId: DolphinGameId.ToString(),
             GameName: Title,
             GamePath: Path,
             Launch: DolphinExecutable,
             LaunchArgs: ROMFile,
             Icon: Cover,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["ReleaseDate"] = [AppLoaderDate is null ? "" : ((DateTime)AppLoaderDate).ToString(CultureInfo.InvariantCulture),],
                 ["Publishers"] = [Publisher ?? "",],
                 ["System"] = [System == (DolphinSystem)(-1) ? "" : System.ToString() ?? "",],
                 ["Region"] = [Region == (DolphinRegion)(-1) ? "" : Region.ToString() ?? "",],
             });
