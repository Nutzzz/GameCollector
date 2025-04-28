using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.Arc;

/// <summary>
/// Handler for finding games installed with Arc.
/// </summary>
/// <remarks>
/// Uses Registry key:
///   HKLM32\SOFTWARE\Perfect World Entertainment\Core
///     or
///   HKCU\Software\Arc\Core
/// </remarks>
/// <remarks>
/// Constructor.
/// </remarks>
/// <param name="registry">
/// The implementation of <see cref="IRegistry"/> to use. For a shared instance
/// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <langword>null</langword>.
/// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
/// of the interface.
/// </param>
/// <param name="fileSystem">
/// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
/// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
/// a custom implementation or just a mock of the interface.
/// </param>
[PublicAPI]
public class ArcHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<ArcGame, ArcGameId>
{
    internal const string ArcRegKey = @"SOFTWARE\Perfect World Entertainment";
    internal const string ArcRegKey2 = @"Software\Arc";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override Func<ArcGame, ArcGameId> IdSelector => game => game.AppId;

    /// <inheritdoc/>
    public override IEqualityComparer<ArcGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(Path.Combine(ArcRegKey, "Arc"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("launcher", out var launcher) && Path.IsPathRooted(launcher))
                    return _fileSystem.FromUnsanitizedFullPath(launcher);
            }

            using var regKey2 = currentUser.OpenSubKey(Path.Combine(ArcRegKey2, "Arc"));
            if (regKey2 is not null)
            {
                if (regKey2.TryGetString("launcher", out var launcher) && Path.IsPathRooted(launcher))
                    return _fileSystem.FromUnsanitizedFullPath(launcher);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<ArcGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        IRegistryKey localMachine;
        if (_registry is not null)
        {
            localMachine = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            var arcPath = new AbsolutePath();
            using (var arcClientKey = localMachine.OpenSubKey(Path.Combine(ArcRegKey, "Arc")))
            {
                if (arcClientKey is not null &&
                    arcClientKey.TryGetString("client", out var clientPath) &&
                    Path.IsPathRooted(clientPath))
                {
                    var path = Path.GetDirectoryName(clientPath);
                    if (path is not null)
                        arcPath = _fileSystem.FromUnsanitizedFullPath(path);
                }
            }


            using var arcGamesKey = localMachine.OpenSubKey(Path.Combine(ArcRegKey, "Core"));
            if (arcGamesKey is null)
            {
                yield return new ErrorMessage($"Unable to open HKEY_LOCAL_MACHINE\\{Path.Combine(ArcRegKey, "Core")}");
                yield break;
            }

            var subKeyNames = arcGamesKey.GetSubKeyNames().ToArray();
            if (subKeyNames.Length == 0)
            {
                yield return new ErrorMessage($"Registry key {arcGamesKey.GetName()} has no sub-keys");
                yield break;
            }

            foreach (var subKeyName in subKeyNames)
            {
                // TODO: Multiple languages may exist for a game (Magic Legends), but just picking English is a poor solution
                if (subKeyName.EndsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    yield return ParseSubKey(arcGamesKey, subKeyName, arcPath, _fileSystem);
                }
            }
        }
    }

    private static OneOf<ArcGame, ErrorMessage> ParseSubKey(IRegistryKey arcKey, string subKeyName, AbsolutePath arcPath, IFileSystem fileSystem)
    {
        try
        {
            using var subKey = arcKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                return new ErrorMessage($"Unable to open {arcKey}\\{subKeyName}");
            }

            var i = subKeyName.IndexOf("en", StringComparison.OrdinalIgnoreCase);
            if (i < 2)
            {
                return new ErrorMessage($"The subkey name of {subKey.GetName()} does not end in \"en\"");
            }

            var sId = subKeyName[..i];
            if (!ulong.TryParse(sId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return new ErrorMessage($"The subkey name of {subKey.GetName()} does not start with a number: \"{sId}\"");
            }

            if (!subKey.TryGetString("INSTALL_PATH", out var path))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"INSTALL_PATH\"");
            }

            var name = "";
            if (path.Contains("_en", StringComparison.OrdinalIgnoreCase))
            {
                name = Path.GetFileName(path[..path.IndexOf("_en", StringComparison.OrdinalIgnoreCase)]);
            }
            else
            {
                name = Path.GetFileName(path.TrimEnd('\\'));
            }
            if (string.IsNullOrEmpty(name))
            {
                return new ErrorMessage($"Name could not be generated from path: \"{path}\"");
            }

            var launch = new AbsolutePath();
            if (subKey.TryGetString("LAUNCHER_PATH", out var launchStr) && Path.IsPathRooted(launchStr))
                launch = fileSystem.FromUnsanitizedFullPath(launchStr);

            var found = false;
            var icon = new AbsolutePath();
            if (subKey.TryGetString("APP_ABBR", out var abbrev))
            {
                icon = arcPath.Combine("resources").Combine("login_pics").Combine(abbrev + ".jpg");
                if (icon.FileExists)
                    found = true;
                else
                {
                    icon = arcPath.Combine("resources")
                        .Combine("passport")
                        .Combine("games")
                        .Combine(abbrev)
                        .Combine("img_game_logo.png");
                    if (icon.FileExists)
                        found = true;
                }
            }
            if (!found)
            {
                if (subKey.TryGetString("CLIENT_PATH", out var client) && Path.IsPathRooted(client))
                    icon = fileSystem.FromUnsanitizedFullPath(client);
                else
                    icon = launch;
            }

            return new ArcGame(
                AppId: ArcGameId.From(id),
                Name: name,
                InstallPath: Path.IsPathRooted(path) ? fileSystem.FromUnsanitizedFullPath(path) : new(),
                LauncherPath: launch,
                Icon: icon);
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {arcKey}\\{subKeyName}");
        }
    }
}
