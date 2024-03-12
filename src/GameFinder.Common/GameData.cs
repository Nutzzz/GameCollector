using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameFinder.Common;

/// <summary>
/// Generic representation of a game.
/// </summary>
/// <param name="Handler"></param>
/// <param name="GameId"></param>
/// <param name="GameName"></param>
/// <param name="GamePath"></param>
/// <param name="SavePath"></param>
/// <param name="Launch"></param>
/// <param name="LaunchArgs"></param>
/// <param name="LaunchUrl"></param>
/// <param name="Icon"></param>
/// <param name="Uninstall"></param>
/// <param name="UninstallArgs"></param>
/// <param name="UninstallUrl"></param>
/// <param name="InstallDate"></param>
/// <param name="LastRunDate"></param>
/// <param name="NumRuns"></param>
/// <param name="RunTime"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsHidden"></param>
/// <param name="HasProblem"></param>
/// <param name="Tags"></param>
/// <param name="MyRating"></param>
/// <param name="BaseGame"></param>
/// <param name="Metadata"></param>
[PublicAPI]
public record GameData(Handlers Handler,
                       string GameId,
                       string GameName,
                       AbsolutePath GamePath,
                       AbsolutePath? SavePath = null,
                       AbsolutePath Launch = new(),
                       string LaunchArgs = "",
                       string LaunchUrl = "",
                       AbsolutePath Icon = new(),
                       AbsolutePath Uninstall = new(),
                       string UninstallArgs = "",
                       string UninstallUrl = "",
                       DateTime? InstallDate = null,
                       DateTime? LastRunDate = null,
                       uint NumRuns = 0,
                       TimeSpan? RunTime = null,
                       bool IsInstalled = true,
                       bool IsHidden = false,
                       bool HasProblem = false,
                       IList<string>? Tags = null,
                       ushort? MyRating = null,
                       string? BaseGame = null,
                       IDictionary<string, List<string>>? Metadata = null) :
    IGame;
