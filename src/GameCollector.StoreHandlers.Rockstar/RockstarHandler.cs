using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.Rockstar;

/// <summary>
/// Handler for finding games installed with Rockstar Games Launcher.
/// </summary>
/// <remarks>
/// Uses registry keys:
///   HKLM32\SOFTWARE\Rockstar Games
///   HKLM32\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
/// </remarks>
[PublicAPI]
public class RockstarHandler : AHandler<RockstarGame, RockstarGameId>
{
    internal const string RockstarKey = @"SOFTWARE\Rockstar Games";
    internal const string UninstallRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <c>null</c>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface.
    /// </param>
    public RockstarHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override IEqualityComparer<RockstarGameId>? IdEqualityComparer => RockstarGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<RockstarGame, RockstarGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(Path.Combine(UninstallRegKey, "Rockstar Games Launcher"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("DisplayIcon", out var icon) && Path.IsPathFullyQualified(icon))
                    return _fileSystem.FromUnsanitizedFullPath(icon);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<RockstarGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

        using var rockstarKey = localMachine32.OpenSubKey(RockstarKey);
        if (rockstarKey is null)
        {
            yield return new ErrorMessage($@"Unable to open HKEY_LOCAL_MACHINE\{RockstarKey}");
            yield break;
        }

        using var unKey = localMachine32.OpenSubKey(UninstallRegKey);
        if (unKey is null)
            yield return new ErrorMessage($"Error opening registry key {UninstallRegKey}");

        var subKeyNames = rockstarKey.GetSubKeyNames().ToArray();
        if (subKeyNames.Length == 0)
        {
            yield return new ErrorMessage($"Registry key {rockstarKey.GetName()} has no sub-keys");
            yield break;
        }

        foreach (var subKeyName in subKeyNames)
        {
            if (!subKeyName.Equals("Launcher", StringComparison.OrdinalIgnoreCase) &&
                !subKeyName.Equals("Rockstar Games Launcher", StringComparison.OrdinalIgnoreCase) &&
                !subKeyName.Equals("Rockstar Games Social Club", StringComparison.OrdinalIgnoreCase))
            {
                if (unKey is not null)
                    yield return ParseRockstarKey(rockstarKey, unKey, subKeyName);
            }
        }
    }

    private OneOf<RockstarGame, ErrorMessage> ParseRockstarKey(IRegistryKey rockstarKey, IRegistryKey unKey, string subKeyName)
    {

        try
        {
            using var subKey = rockstarKey.OpenSubKey(subKeyName);
            if (subKey is null)
                return new ErrorMessage($"Unable to open {rockstarKey}\\{subKeyName}");

            if (!subKey.TryGetString("InstallFolder", out var strPath))
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"InstallFolder\"");

            if (!Path.IsPathFullyQualified(strPath))
                return new ErrorMessage($"{strPath} is not a valid path");

            return ParseUninstallKey(unKey, subKeyName, _fileSystem.FromUnsanitizedFullPath(strPath));
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {rockstarKey}\\{subKeyName}");
        }
    }

    private OneOf<RockstarGame, ErrorMessage> ParseUninstallKey(IRegistryKey uninstallKey, string strId, AbsolutePath path)
    {
        try
        {
            AbsolutePath exe = default;
            AbsolutePath uninst = default;
            var uninstArgs = "";

            var subKeyNames = uninstallKey.GetSubKeyNames().ToArray();
            foreach (var subKeyName in subKeyNames)
            {
                using var subKey = uninstallKey.OpenSubKey(subKeyName);
                if (subKey is null)
                    continue;

                if (subKey.TryGetString("InstallLocation", out var loc) &&
                    loc.Equals(path.GetFullPath(), StringComparison.OrdinalIgnoreCase))
                {
                    subKey.TryGetString("DisplayName", out var name);
                    subKey.TryGetString("DisplayIcon", out var strExe);
                    subKey.TryGetString("HelpLink", out var help);
                    subKey.TryGetString("Publisher", out var pub);
                    subKey.TryGetString("Readme", out var read);
                    subKey.TryGetString("UninstallString", out var strUninst);
                    subKey.TryGetString("URLInfoAbout", out var info);

                    if (!string.IsNullOrEmpty(strUninst))
                    {
                        if (strUninst.Contains("\" ", StringComparison.Ordinal))
                        {
                            var uninstExe = strUninst[..strUninst.IndexOf("\" ", StringComparison.Ordinal)].Trim('\"');
                            if (Path.IsPathFullyQualified(uninstExe))
                            {
                                uninst = _fileSystem.FromUnsanitizedFullPath(uninstExe);
                                uninstArgs = strUninst[(strUninst.IndexOf("\" ", StringComparison.Ordinal) + 2)..];
                            }
                            if (string.IsNullOrEmpty(strId) && strUninst.Contains("-uninstall=", StringComparison.Ordinal))
                                strId = strUninst[..(strUninst.LastIndexOf("-uninstall=", StringComparison.Ordinal) + 11)];
                        }
                    }
                    else if (string.IsNullOrEmpty(strId))
                        strId = path.FileName;

                    if (string.IsNullOrEmpty(name))
                        name = strId;
                    if (string.IsNullOrEmpty(strExe))
                        exe = Utils.FindExe(path, _fileSystem, name);

                    return new RockstarGame(
                        Id: RockstarGameId.From(strId),
                        Name: name ?? "",
                        InstallFolder: path,
                        Launch: exe,
                        Uninstall: uninst,
                        UninstallArgs: uninstArgs,
                        NotFoundOnDisk: exe == default,
                        Publisher: pub ?? "",
                        UrlInfoAbout: info ?? read ?? "",
                        HelpLink: help ?? "");
                }
            }

            // InstallFolder not found in Uninstall registry
            exe = Utils.FindExe(path, _fileSystem);

            return new RockstarGame(
                Id: RockstarGameId.From(strId),
                Name: strId ?? "",
                InstallFolder: path,
                Launch: exe,
                NotFoundOnDisk: exe == default);
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {uninstallKey}");
        }
    }
}
