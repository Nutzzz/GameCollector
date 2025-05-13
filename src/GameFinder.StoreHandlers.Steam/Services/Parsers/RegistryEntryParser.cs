using System;
using System.IO;
using FluentResults;
using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.Steam.Models;
using GameCollector.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Steam.Services;

/// <summary>
/// Parser for Steam Uninstall registry entries.
/// </summary>
/// <seealso cref="RegistryEntry"/>
[PublicAPI]
public static class RegistryEntryParser
{
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>
    /// Parses the registry entry for the given Steam app ID.
    /// </summary>
    public static Result<RegistryEntry> ParseRegistryEntry(AppId appId, IFileSystem fileSystem, IRegistry? registry = null)
    {
        RegistryEntry regEntry;

        if (fileSystem is null)
        {
            return Result.Ok();
            //return Result.Fail(new Error("Invalid filesystem parameter!"));
        }
        if (registry is null)
        {
            return Result.Ok();
            //return Result.Fail(new Error("Invalid registry parameter!"));
        }

        IRegistryKey? subKey = default;

        try
        {
            // Entries are usually in HKLM64, but occasionally HKLM32 (or both)
            var localMachine64 = registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var localMachine32 = registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var subKey64 = localMachine64.OpenSubKey(Path.Combine(UninstallRegKey, "Steam App " + appId));
            using var subKey32 = localMachine32.OpenSubKey(Path.Combine(UninstallRegKey, "Steam App " + appId));

            subKey = subKey64;
            if (subKey64 is null || string.IsNullOrEmpty(subKey64.GetString("InstallLocation")) &&
                subKey32 is not null && !string.IsNullOrEmpty(subKey32.GetString("InstallLocation")))
            {
                subKey = subKey32;
            }

            if (subKey is null)
            {
                return Result.Ok();
                /*
                return Result.Fail(
                    new Error("Invalid registry key!")
                        .WithMetadata("AppId", appId)
                        .WithMetadata("Key", subKey?.ToString())
                );
                */
            }

            var strIcon = subKey.GetString("DisplayIcon");
            var strLoc = subKey.GetString("InstallLocation");
            var strUninst = subKey.GetString("UninstallString");
            var strUnExe = "";
            var strUnParam = "";
            if (strUninst is not null)
            {
                if (strUninst.StartsWith('"'))
                {
                    strUnExe = strUninst[..strUninst.LastIndexOf('"')];
                    strUnParam = strUninst[(strUninst.LastIndexOf('"') + 1)..];
                }
                else if (strUninst.Contains(' ', StringComparison.Ordinal))
                {
                    strUnExe = strUninst[..strUninst.IndexOf(' ', StringComparison.Ordinal)];
                    strUnParam = strUninst[(strUninst.IndexOf(' ', StringComparison.Ordinal) + 1)..];
                }
                else
                {
                    strUnExe = strUninst;
                }
            }
            regEntry = new()
            {
                AppId = appId,
                RegistryPath = subKey,
                DisplayIcon = (!string.IsNullOrEmpty(strIcon) && Path.IsPathFullyQualified(strIcon)) ?
                    fileSystem.FromUnsanitizedFullPath(strIcon) : null,
                DisplayName = subKey.GetString("DisplayName") ?? "",
                HelpLink = subKey.GetString("HelpLink") ?? "",
                InstallLocation = (!string.IsNullOrEmpty(strLoc) && Path.IsPathFullyQualified(strLoc)) ?
                    fileSystem.FromUnsanitizedFullPath(strLoc) : null,
                Publisher = subKey.GetString("Publisher") ?? "",
                UninstallExecutable = Path.IsPathFullyQualified(strUnExe) ? fileSystem.FromUnsanitizedFullPath(strUnExe) : null,
                UninstallParameters = strUnParam,
                URLInfoAbout = subKey.GetString("URLInfoAbout") ?? "",
            };

            return regEntry;
        }
        catch (Exception ex)
        {
            return Result.Ok();
            /*
            return Result.Fail(
                new ExceptionalError("Exception was thrown while parsing the registry!", ex)
                    .WithMetadata("Key", subKey?.ToString())
            );
            */
        }
    }
}
