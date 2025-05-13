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

namespace GameCollector.StoreHandlers.GOG;

/// <summary>
/// Handler for finding games installed with GOG Galaxy.
/// </summary>
[PublicAPI]
public partial class GOGHandler : AHandler<GOGGame, GOGGameId>
{
    internal const string GOGRegKey = @"Software\GOG.com\Games";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. For tests either use
    /// <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface. See the README for more information if you want to use
    /// Wine.
    /// </param>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface. See the README for more information
    /// if you want to use Wine.
    /// </param>
    public GOGHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override Func<GOGGame, GOGGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<GOGGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine.OpenSubKey(@"SOFTWARE\GOG.com\GalaxyClient");
            if (regKey is null) return default;

            if (regKey.TryGetString("clientExecutable", out var clientExe))
            {
                using var regKey2 = regKey.OpenSubKey("paths");
                if (regKey2 is null) return default;

                if (regKey2.TryGetString("client", out var clientPath) && Path.IsPathFullyQualified(clientPath))
                    return _fileSystem.FromUnsanitizedFullPath(clientPath).Combine(clientExe);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<GOGGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        Dictionary<GOGGameId, OneOf<GOGGame, ErrorMessage>> games = [];

        try
        {
            var localMachine = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var gogKey = localMachine.OpenSubKey(GOGRegKey);
            if (gogKey is null)
            {
                return new OneOf<GOGGame, ErrorMessage>[]
                {
                    new ErrorMessage($"Unable to open HKEY_LOCAL_MACHINE\\{GOGRegKey}"),
                };
            }

            var subKeyNames = gogKey.GetSubKeyNames().ToArray();
            if (subKeyNames.Length == 0)
            {
                return new OneOf<GOGGame, ErrorMessage>[]
                {
                    new ErrorMessage($"Registry key {gogKey.GetName()} has no sub-keys"),
                };
            }

            Dictionary<GOGGameId, OneOf<GOGGame, ErrorMessage>> installedGames = [];
            foreach (var subKeyName in subKeyNames)
            {
                var regGame = ParseSubKey(gogKey, subKeyName, settings?.BaseOnly);
                GOGGameId id = default;
                if (regGame.IsT0)
                    id = regGame.AsT0.Id;
                _ = installedGames.TryAdd(id, regGame);
            }

            foreach (var dbGame in FindGamesFromDatabase(settings).ToDictionary())
            {
                if (dbGame.Value.IsT0)
                {
                    var reg = dbGame.Value.AsT0;
                    if (installedGames.TryGetValue(dbGame.Key, out var installed))
                    {
                        var db = installed.AsT0;
                        games.Add(dbGame.Key, new GOGGame(
                            Id: dbGame.Key,
                            Name: db.Name,
                            Path: db.Path == default ? reg.Path : db.Path,
                            BuildId: db.BuildId,
                            Launch: db.Launch == default ? (reg.Exe.FileExists ? reg.Exe : new()) : db.Launch,
                            LaunchParam: string.IsNullOrEmpty(db.LaunchParam) ? reg.LaunchParam : db.LaunchParam,
                            LaunchUrl: db.LaunchUrl,
                            Exe: db.Exe == default ? reg.Exe : db.Exe,
                            UninstallCommand: reg.UninstallCommand,
                            InstallDate: db.InstallDate,
                            LastPlayedDate: db.LastPlayedDate,
                            IsInstalled: db.IsInstalled,
                            IsOwned: db.IsOwned,
                            IsHidden: db.IsHidden,
                            Tags: db.Tags,
                            MyRating: db.MyRating,
                            ReleaseDate: db.ReleaseDate,
                            StoreUrl: db.StoreUrl,
                            SupportUrl: db.SupportUrl,
                            BoxArtUrl: db.BoxArtUrl,
                            LogoUrl: db.LogoUrl,
                            IconUrl: db.IconUrl));
                    }
                    else
                        games.Add(dbGame.Key, dbGame.Value);
                }
                else
                    _ = games.TryAdd(default, dbGame.Value);
            }

            foreach (var installed in installedGames)
            {
                if (installed.Value.IsT0)
                {
                    if (!games.ContainsKey(installed.Key))
                        games.Add(installed.Key, installed.Value);
                }
                else
                    _ = games.TryAdd(default, installed.Value);
            }

            return games.Values;
        }
        catch (Exception e)
        {
            return new OneOf<GOGGame, ErrorMessage>[]
            {
                new ErrorMessage(e, "Exception looking for GOG games"),
            };
        }
    }

    private OneOf<GOGGame, ErrorMessage> ParseSubKey(IRegistryKey gogKey, string subKeyName, bool? baseOnly)
    {
        try
        {
            using var subKey = gogKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                return new ErrorMessage($"Unable to open {gogKey}\\{subKeyName}");
            }

            if (!subKey.TryGetString("gameID", out var sId))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"gameID\"");
            }

            if (!long.TryParse(sId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lId))
            {
                return new ErrorMessage($"The value \"gameID\" of {subKey.GetName()} is not a number: \"{sId}\"");
            }
            var id = GOGGameId.From(lId);

            if (!subKey.TryGetString("gameName", out var name))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"gameName\"");
            }

            if (!subKey.TryGetString("path", out var path))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"path\"");
            }

            if (!subKey.TryGetString("buildId", out var buildId))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"buildId\"");
            }

            GOGGameId parentId = default;
            subKey.TryGetString("dependsOn", out var sParent);
            if (!string.IsNullOrEmpty(sParent))
            {
                if (baseOnly == true)
                    return new ErrorMessage($"{subKey.GetName()} is a DLC");

                if (long.TryParse(sParent, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lParent))
                    parentId = GOGGameId.From(lParent);
            }
            subKey.TryGetString("exe", out var exe);
            //subKey.TryGetString("launchCommand", out var launch);
            subKey.TryGetString("launchParam", out var launchParam);
            subKey.TryGetString("uninstallCommand", out var uninst);
            subKey.TryGetString("supportLink", out var support);
            if (!string.IsNullOrEmpty(support) && !support.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                support = "http://www.gog.com/support/" + support;

            AbsolutePath exePath = default;
            if (exe is not null)
                exePath = Path.IsPathFullyQualified(exe) ? _fileSystem.FromUnsanitizedFullPath(exe) : new();

            return new GOGGame(
                Id: id,
                Name: name,
                Path: Path.IsPathFullyQualified(path) ? _fileSystem.FromUnsanitizedFullPath(path) : new(),
                BuildId: buildId,
                Launch: exePath,
                LaunchUrl: $"goggalaxy://openGameView/{sId}",
                LaunchParam: launchParam ?? "",
                Exe: exePath,
                UninstallCommand: (!string.IsNullOrEmpty(uninst) && Path.IsPathFullyQualified(uninst)) ?
                    _fileSystem.FromUnsanitizedFullPath(uninst) : new(),
                IsInstalled: exePath != default && exePath.FileExists,
                IsOwned: true,
                SupportUrl: support ?? "",
                ParentId: parentId
            );
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {gogKey}\\{subKeyName}");
        }
    }
}
