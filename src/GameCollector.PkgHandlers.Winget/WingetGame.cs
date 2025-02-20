using System;
using System.Collections.Generic;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.PkgHandlers.Winget;

/// <summary>
/// Represents an installed app or available package via Windows Package Manager.
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="InstallDirectory"></param>
/// <param name="Launch"></param>
/// <param name="InstallerUrl"></param>
/// <param name="Uninstall"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsOwned"></param>
/// <param name="InstallDate"></param>
/// <param name="Description"></param>
/// <param name="Publisher"></param>
/// <param name="PackageTags"></param>
/// <param name="PublisherUrl"></param>
/// <param name="SupportUrl"></param>
/// <param name="Homepage"></param>
/// <param name="Author"></param>
/// <param name="PackageName"></param>
/// <param name="Moniker"></param>
/// <param name="Source"></param>
/// <param name="LicenseType"></param>
/// <param name="InstalledVersion"></param>
/// <param name="DefaultVersion"></param>
/// <param name="AgeRating"></param>

[PublicAPI]
public record WingetGame(WingetGameId Id,
                         string? Name,
                         AbsolutePath InstallDirectory,
                         AbsolutePath Launch = new(),
                         string? InstallerUrl = "",
                         AbsolutePath Uninstall = new(),
                         bool IsInstalled = true,
                         bool IsOwned = true,
                         DateTime? InstallDate = null,
                         string? Description = "",
                         string? Publisher = "",
                         List<string>? PackageTags = default,
                         string? PublisherUrl = "",
                         string? SupportUrl = "",
                         string? Homepage = "",
                         string? Author = "",
                         string? PackageName = "",
                         string? Moniker = "",
                         string? Source = "",
                         string? LicenseType = "",
                         string? InstalledVersion = "",
                         string? DefaultVersion = "",
                         string? AgeRating = "") :
    GameData(Handler: Handler.PkgHandler_Winget,
             GameId: Id.ToString(),
             GameName: Name ?? "",
             GamePath: InstallDirectory,
             Launch: Launch,
             LaunchUrl: InstallerUrl ?? "",
             Icon: Launch,
             Uninstall: Uninstall,
             IsInstalled: IsInstalled,
             IsOwned: IsOwned,
             InstallDate: InstallDate,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["Description"] = new() { Description ?? "", },
                 ["Developers"] = new() { Author ?? "", },
                 ["Publishers"] = new() { Publisher ?? "", },
                 ["Genres"] = PackageTags ?? new(),
                 ["WebInfo"] = new() { Homepage ?? PublisherUrl ?? "" },
                 ["WebSupport"] = new() { SupportUrl ?? "" },
                 ["PackageName"] = new() { PackageName ?? "" },
                 ["Moniker"] = new() { Moniker ?? "", },
                 ["Source"] = new() { Source ?? "", },
                 ["License"] = new() { LicenseType ?? "", },
                 ["InstalledVersion"] = new() { InstalledVersion ?? "", },
                 ["DefaultVersion"] = new() { DefaultVersion ?? "", },
                 ["AgeRating"] = new() { AgeRating ?? "", },
             });
