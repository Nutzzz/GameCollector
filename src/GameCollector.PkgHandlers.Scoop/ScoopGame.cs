using System;
using System.Collections.Generic;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.PkgHandlers.Scoop;

/// <summary>
/// Represents an installed app or available package via Scoop.
/// </summary>
/// <param name="Name"></param>
/// <param name="Prefix"></param>
/// <param name="Binary"></param>
/// <param name="Problems"></param>
/// <param name="IsInstalled"></param>
/// <param name="UpdateDate"></param>
/// <param name="InstallVersion"></param>
/// <param name="Description"></param>
/// <param name="Website"></param>
/// <param name="Bucket"></param>
/// <param name="DefaultVersion"></param>
/// <param name="DownloadUrl"></param>
/// <param name="LicenseType"></param>
/// <param name="Notes"></param>
/// <param name="InstallInfo"></param>

[PublicAPI]
public record ScoopGame(ScoopGameId Name,
                        string? ShortcutName,
                        AbsolutePath Prefix,
                        AbsolutePath Binary,
                        bool IsInstalled = true,
                        DateTime? UpdateDate = null,
                        IList<Problem>? Problems = null,
                        string? InstallVersion = "",
                        string? Description = "",
                        string? Website = "",
                        string? Bucket = "",
                        string? DefaultVersion = "",
                        string? DownloadUrl = "",
                        string? LicenseType = "",
                        string? Notes = "",
                        string? InstallInfo = "") :
    GameData(Handler: Handler.PkgHandler_Scoop,
             GameId: Name.ToString(),
             GameName: Name.ToString(),
             GamePath: Prefix,
             Launch: Binary,
             IsInstalled: IsInstalled,
             UpdateDate: UpdateDate,
             Problems: Problems,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = [Description ?? "",],
                 ["Genres"] = [Bucket ?? ""],
                 ["WebInfo"] = [Website ?? ""],
                 ["DownloadUrl"] = [DownloadUrl ?? "",],
                 ["License"] = [LicenseType ?? "",],
                 ["InstallVersion"] = [InstallVersion ?? "",],
                 ["DefaultVersion"] = [DefaultVersion ?? "",],
                 ["Notes"] = [Notes ?? "",],
                 ["InstallInfo"] = [InstallInfo ?? "",],
             });
