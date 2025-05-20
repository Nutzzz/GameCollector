using System;
using System.Collections.Generic;
using System.Globalization;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.PkgHandlers.Choco;

/// <summary>
/// Represents an installed app or available package via Chocolatey.
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="InstallLocation"></param>
/// <param name="Uninstall"></param>
/// <param name="UninstallArgs"></param>
/// <param name="PkgUrl"></param>
/// <param name="IsInstalled"></param>
/// <param name="RemoveOnly"></param>
/// <param name="InstalledVersion"></param>
/// <param name="DefaultVersion"></param>
/// <param name="PublishDate"></param>
/// <param name="Notes"></param>
/// <param name="Approved"></param>
/// <param name="Cached"></param>
/// <param name="Broken"></param>
/// <param name="NumDownloads"></param>
/// <param name="VerDownloads"></param>
/// <param name="PkgTags"></param>
/// <param name="PkgSource"></param>
/// <param name="SoftwareSite"></param>
/// <param name="Documentation"></param>
/// <param name="MailingList"></param>
/// <param name="Issues"></param>
/// <param name="SoftwareLicense"></param>
/// <param name="SoftwareSource"></param>
/// <param name="Summary"></param>
/// <param name="Description"></param>
/// <param name="ReleaseNotes"></param>

[PublicAPI]
public record ChocoGame(ChocoGameId Id,
                         string? Title,
                         AbsolutePath InstallLocation,
                         string? PkgUrl = "",
                         AbsolutePath Uninstall = new(),
                         string? UninstallArgs = "",
                         bool IsInstalled = true,
                         bool? RemoveOnly = false,
                         string? InstalledVersion = "",
                         string? DefaultVersion = "",
                         DateTime? PublishDate = default,
                         string? Notes = "",
                         bool? Approved = false,
                         bool? Cached = false,
                         bool? Broken = false,
                         ulong? NumDownloads = 0,
                         ulong? VerDownloads = 0,
                         string? PkgSource = "",
                         List<string>? PkgTags = default,
                         string? SoftwareSite = "",
                         string? Documentation = "",
                         string? MailingList = "",
                         string? Issues = "",
                         string? SoftwareLicense = "",
                         string? SoftwareSource = "",
                         string? Summary = "",
                         string? Description = "",
                         string? ReleaseNotes = "") :
    GameData(Handler: Handler.PkgHandler_Choco,
             GameId: Id.ToString(),
             GameName: Title ?? "",
             GamePath: InstallLocation,
             LaunchUrl: PkgUrl ?? "",
             Uninstall: Uninstall,
             UninstallArgs: UninstallArgs ?? "",
             IsInstalled: IsInstalled,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["RemoveOnly"] = [RemoveOnly?.ToString() ?? ""],
                 ["PublishDate"] = [PublishDate?.ToString(CultureInfo.InvariantCulture) ?? ""],
                 ["Notes"] = [Notes ?? "",],
                 ["Approved"] = [Approved?.ToString() ?? ""],
                 ["Cached"] = [Cached?.ToString() ?? ""],
                 ["Broken"] = [Broken?.ToString() ?? ""],
                 ["NumDownloads"] = [NumDownloads?.ToString(CultureInfo.InvariantCulture) ?? ""],
                 ["VerDownloads"] = [VerDownloads?.ToString(CultureInfo.InvariantCulture) ?? ""],
                 ["Source"] = [PkgSource ?? "",],
                 ["Description"] = [Summary ?? "",],
                 ["LongDescription"] = [Description ?? "",],
                 ["ReleaseNotes"] = [ReleaseNotes ?? "",],
                 ["Genres"] = PkgTags ?? [],
                 ["WebInfo"] = [SoftwareSite ?? ""],
                 ["WebSupport"] = [Documentation ?? Issues ?? MailingList ?? ""],
                 ["SourceCode"] = [SoftwareSource ?? "",],
                 ["License"] = [SoftwareLicense ?? "",],
                 ["InstalledVersion"] = [InstalledVersion ?? "",],
                 ["DefaultVersion"] = [DefaultVersion ?? "",],
             });
