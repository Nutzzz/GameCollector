using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameCollector.Common;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.Legacy;

/// <summary>
/// Handler for finding games installed with Legacy Games Launcher.
/// </summary>
/// <remarks>
/// Constructor. Uses json file:
///   %AppData%\legacy-games-launcher\app-state.json
/// and Registry key:
///   HKCU\Software\Legacy Games
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
public class LegacyHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<LegacyGame, LegacyGameId>
{
    internal const string LegacyRegKey = @"Software\Legacy Games";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = SourceGenerationContext.Default,
        };

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override IEqualityComparer<LegacyGameId>? IdEqualityComparer => LegacyGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<LegacyGame, LegacyGameId> IdSelector => game => game.InstallerUuid;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine64 = _registry.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

            using var regKey = localMachine64.OpenSubKey(Path.Combine(UninstallRegKey, "da414c81-a9fd-5732-bd5e-8acced116298"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("DisplayIcon", out var icon) && Path.IsPathFullyQualified(icon))
                {
                    return _fileSystem.FromUnsanitizedFullPath(icon).Parent;
                }
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<LegacyGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        List<OneOf<LegacyGame, ErrorMessage>> games = [];
        var regDict = ParseRegistry();
        var jsonDict = ParseJsonFile(regDict.Keys);
        var instDirs = new List<AbsolutePath>();

        foreach (var regGame in regDict)
        {
            if (regGame.Value.IsT1)
            {
                games.Add(regGame.Value.AsT1);
                continue;
            }

            var game = regGame.Value.AsT0;
            if (jsonDict.TryGetValue(regGame.Key, out var jsonGame) && jsonGame.IsT0)
            {
                games.Add(new LegacyGame(
                    game.InstallerUuid,
                    game.ProductName,
                    game.InstDir,
                    game.ExePath,
                    game.DisplayIcon,
                    game.UninstallString,
                    IsInstalled: true,
                    IsOwned: true,
                    Description: jsonGame.AsT0.Description,
                    Publisher: game.Publisher,
                    Genre: jsonGame.AsT0.Genre,
                    CoverArtUrl: jsonGame.AsT0.CoverArtUrl));
                if (game.InstDir != default)
                    instDirs.Add(game.InstDir);
                continue;
            }

            games.Add(game);
            if (game.InstDir != default)
                instDirs.Add(game.InstDir);
        }

        foreach (var jsonGame in jsonDict)
        {
            if (!regDict.ContainsKey(jsonGame.Key))
            {
                if (jsonGame.Value.IsT1)
                {
                    games.Add(jsonGame.Value.AsT1);
                    continue;
                }

                var game = jsonGame.Value.AsT0;
                if (instDirs.Contains(game.InstDir))
                    continue;

                games.Add(game);
            }
        }

        return games;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private Dictionary<LegacyGameId, OneOf<LegacyGame, ErrorMessage>> ParseJsonFile(Dictionary<LegacyGameId, OneOf<LegacyGame, ErrorMessage>>.KeyCollection searchIds, bool unowned = false)
    {
        Dictionary<LegacyGameId, OneOf<LegacyGame, ErrorMessage>> gameDict = [];

        var jsonFile = GetLegacyJsonFile();
        if (!jsonFile.FileExists)
            return gameDict;
        List<AbsolutePath> libraryPaths = [];

        try
        {
            using var stream = jsonFile.Read();
            var appState = JsonSerializer.Deserialize<AppStateFile>(stream, JsonSerializerOptions);
            if (appState is null)
                return gameDict;

            if (appState.SiteData is null || appState.SiteData.Catalog is null)
                return gameDict;

            foreach (var item in appState.SiteData.Catalog)
            {
                if (item.Games is null)
                    continue;

                foreach (var catalogGame in item.Games)
                {
                    var isOwned = true;
                    var id = LegacyGameId.From(catalogGame.InstallerUuid ?? "");
                    if (!searchIds.Contains(id))
                    {
                        if (!unowned)
                            continue;
                        isOwned = false;
                    }

                    var genre = Genre.Unknown;
                    if (item.Categories is not null)
                    {
                        foreach (var category in item.Categories)
                        {
                            foreach (var val in Enum.GetValues<Genre>())
                            {
                                if (category.Id is not null && ((Genre)category.Id).Equals(val))
                                {
                                    genre = val;
                                    break;
                                }
                            }
                            if (genre == Genre.Unknown)
                                break;
                        }
                    }

                    gameDict.TryAdd(id, new LegacyGame(
                        id,
                        catalogGame.GameName ?? "",
                        InstDir: default,
                        IsInstalled: false,
                        IsOwned: isOwned,
                        Description: catalogGame.GameDescription ?? "",
                        Genre: genre,
                        CoverArtUrl: catalogGame.GameCoverart ?? ""));
                }
            }

            if (appState.SiteData.GiveawayCatalog is null)
                return gameDict;

            foreach (var giveaway in appState.SiteData.GiveawayCatalog)
            {
                if (giveaway.Games is null)
                    continue;

                foreach (var giveawayGame in giveaway.Games)
                {
                    var id = LegacyGameId.From(giveawayGame.InstallerUuid ?? "");
                    gameDict.TryAdd(id, new LegacyGame(
                        id,
                        giveawayGame.GameName ?? "",
                        InstDir: default,
                        IsInstalled: false,
                        Description: giveawayGame.GameDescription ?? "",
                        Genre: Genre.Unknown,
                        CoverArtUrl: giveawayGame.GameCoverart ?? ""));
                }
            }

            if (appState.Settings is not null)
            {
                foreach (var path in appState.Settings.GameLibraryPath.EnumerateArray())
                {
                    libraryPaths.Add(_fileSystem.FromUnsanitizedFullPath(path.ToString()));
                }
            }
        }
        catch (Exception e)
        {
            gameDict.TryAdd(LegacyGameId.From(""), new ErrorMessage(e, $"Exception parsing Legacy Games json file {jsonFile}"));
        }

        var i = 0;
        foreach (var game in GetInstalledGames(libraryPaths))
        {
            if (game.IsT1)
            {
                i++;
                gameDict.Add(LegacyGameId.From($"i{i.ToString(CultureInfo.InvariantCulture)}"), game);
                continue;
            }

            gameDict.Add(game.AsT0.InstallerUuid, game);
        }

        return gameDict;
    }

    private List<OneOf<LegacyGame, ErrorMessage>> GetInstalledGames(List<AbsolutePath> libPaths)
    {
        List<OneOf<LegacyGame, ErrorMessage>> games = [];

        foreach (var lib in libPaths)
        {
            try
            {
                foreach (var path in lib.EnumerateDirectories(recursive: false))
                {
                    var strPath = path.FileName;
                    var id = LegacyGameId.From(strPath);
                    if (strPath.Equals("Legacy Games Launcher", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var exe = Utils.FindExe(path, _fileSystem);
                    games.Add(new LegacyGame(
                        id,
                        path.FileName,
                        path,
                        exe,
                        IsInstalled: true,
                        IsOwned: true,
                        // TODO: I don't remember, should NotFoundInData always be true here?
                        Problems: MakeProblemsList(NotFoundInData: true, NotFoundOnDisk: exe == default)));
                }
            }
            catch (Exception e)
            {
                games.Add(new ErrorMessage(e, $"Exception parsing Legacy Games library {lib.GetFullPath()}"));
            }
        }

        return games;
    }

    private (string icon, string uninst, string pub) ParseUninstall(string subKeyName)
    {
        try
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var unKey = localMachine32.OpenSubKey(Path.Combine(UninstallRegKey, subKeyName));
            if (unKey is null)
                return new();

            unKey.TryGetString("DisplayIcon", out var icon);
            unKey.TryGetString("UninstallString", out var uninst);
            unKey.TryGetString("Publisher", out var pub);

            return (icon ?? "", uninst ?? "", pub ?? "");
        }
        catch (Exception) { }

        return new();
    }

    private OneOf<LegacyGame, ErrorMessage> ParseSubKey(IRegistryKey legKey, string subKeyName)
    {
        try
        {
            using var subKey = legKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                return new ErrorMessage($"Unable to open {legKey}\\{subKeyName}");
            }

            if (!subKey.TryGetString("InstallerUUID", out var id))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"InstallerUUID\"");
            }

            if (!subKey.TryGetString("ProductName", out var name))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"ProductName\"");
            }

            if (!subKey.TryGetString("InstDir", out var path))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"InstDir\"");
            }

            AbsolutePath instDir = new();
            AbsolutePath exePath = new();
            if (Path.IsPathFullyQualified(path))
            {
                instDir = _fileSystem.FromUnsanitizedFullPath(path);
                if (subKey.TryGetString("GameExe", out var exe))
                {
                    exePath = instDir.Combine(exe);
                }
            }

            var (icon, uninstall, publisher) = ParseUninstall(name);

            return new LegacyGame(
                InstallerUuid: LegacyGameId.From(id),
                ProductName: name,
                InstDir: instDir,
                ExePath: exePath,
                DisplayIcon: Path.IsPathFullyQualified(icon) ? _fileSystem.FromUnsanitizedFullPath(icon) : exePath,
                UninstallString: Path.IsPathFullyQualified(uninstall) ? _fileSystem.FromUnsanitizedFullPath(uninstall) : new(),
                IsInstalled: true,
                Publisher: publisher
            );
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {legKey}\\{subKeyName}");
        }
    }

    private Dictionary<LegacyGameId, OneOf<LegacyGame, ErrorMessage>> ParseRegistry()
    {
        if (_registry is null)
        {
            return new() { [LegacyGameId.From("")] = new ErrorMessage("Unable to open registry"), };
        }

        Dictionary<LegacyGameId, OneOf<LegacyGame, ErrorMessage>> gameDict = [];

        try
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var legKey = currentUser.OpenSubKey(LegacyRegKey);
            if (legKey is null)
            {
                return new() { [LegacyGameId.From("")] = new ErrorMessage($"Unable to open HKEY_CURRENT_USER\\{LegacyRegKey}"), };
            }

            var subKeyNames = legKey.GetSubKeyNames().ToArray();
            if (subKeyNames.Length == 0)
            {
                return new() { [LegacyGameId.From("")] = new ErrorMessage($"Registry key {legKey.GetName()} has no sub-keys"), };
            }

            var i = 0;
            foreach (var subKey in subKeyNames.Select(subKeyName => ParseSubKey(legKey, subKeyName)).ToArray())
            {
                if (subKey.IsT1)
                {
                    i++;
                    gameDict.TryAdd(LegacyGameId.From($"r{i.ToString(CultureInfo.InvariantCulture)}"), subKey);
                }
                gameDict.TryAdd(subKey.AsT0.InstallerUuid, subKey);
            }
        }
        catch (Exception e)
        {
            gameDict.TryAdd(LegacyGameId.From(""), new ErrorMessage(e, "Exception looking for Legacy games in registry"));
        }

        return gameDict;
    }

    public AbsolutePath GetLegacyJsonFile()
    {
        return _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .Combine("legacy-games-launcher")
            .Combine("app-state.json");
    }

    private static List<Problem> MakeProblemsList(bool NotFoundInData, bool NotFoundOnDisk)
    {
        List<Problem> probs = [];

        if (NotFoundInData)
            probs.Add(Problem.NotFoundInData);
        if (NotFoundOnDisk)
            probs.Add(Problem.NotFoundOnDisk);

        return probs;
    }
}
