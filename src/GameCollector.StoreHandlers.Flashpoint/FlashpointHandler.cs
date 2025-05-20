using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.SQLiteUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using System.Threading;
using System.Globalization;
using System.Data;

namespace GameCollector.StoreHandlers.Flashpoint;

/// <summary>
/// Handler for finding games installed with Flashpoint launcher.
/// </summary>
/// <remarks>
/// Constructor. Uses SQLite database:
///   [Install Dir]\Data\flashpoint.sqlite
/// and json file:
///   [Install Dir]\preferences.json
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
public class FlashpointHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<FlashpointGame, FlashpointGameId>
{
    internal const string FlashpointUrl = "flashpoint://";  // flashpoint://<gameid>

    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = SourceGenerationContext.Default,
        };

    private readonly IRegistry? _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override Func<FlashpointGame, FlashpointGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<FlashpointGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var classRoot = _registry.OpenBaseKey(RegistryHive.ClassesRoot);

            using var regKey = classRoot.OpenSubKey(Path.Combine("flashpoint", "shell", "open", "command"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("", out var exePath))
                {
                    return _fileSystem.FromUnsanitizedFullPath(exePath[..^5]); // Remove ' "%1"'
                }
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<FlashpointGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var database = GetDatabaseFilePath();
        if (!database.FileExists)
        {
            yield return new ErrorMessage($"The database file {database} does not exist!");
            yield break;
        }

        foreach (var game in ParseDatabase(database, settings))
        {
            yield return game;
        }
    }

    private IEnumerable<OneOf<FlashpointGame, ErrorMessage>> ParseDatabase(AbsolutePath database, Settings? settings = null)
    {
        var games = SQLiteHelpers.GetDataTable(database, "SELECT * FROM game;").ToList<Game>();
        if (games is null)
        {
            yield return new ErrorMessage($"Could not deserialize file {database}");
            yield break;
        }

        //var addApps = SQLiteHelpers.GetDataTable(database, "SELECT * FROM additional_app;").ToList<AdditionalApp>();
        var dataPacks = GetDataPacks(database);
        var extremeTags = GetExtremeTags();
        //var platforms = GetPlatformAliases();

        foreach (var game in games)
        {
            var strId = game.Id ?? "";
            var id = FlashpointGameId.From(strId);
            if (string.IsNullOrEmpty(strId))
            {
                yield return new ErrorMessage($"Value for \"id\" does not exist in table \"game\" in file {database}");
                continue;
            }

            var title = game.Title ?? "";
            List<string> tags = [.. (game.TagsStr ?? "").Split("; ")];
            foreach (var tag in tags)
            {
                if (extremeTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    yield return new ErrorMessage($"\"{title}\" is ignored because it has a tag that is filtered by ${GetPrefsFilePath()}.");
                    continue;
                }
            }

            var lib = game.Library ?? "";       // "arcade" [Games] or "theatre" [Animations]
            if (settings?.GamesOnly == true && !lib.Equals("arcade", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ErrorMessage($"\"{title}\" is not a game (i.e., an animation)!");
                continue;
            }

            List<string> status = [.. (game.Status ?? "").Split("; ")];  // "Playable", "Hacked", and/or "Partial"
            if (!status.Contains("Playable", StringComparer.OrdinalIgnoreCase) && settings?.PlayableOnly == true)
            {
                yield return new ErrorMessage($"\"{title}\" is not yet playable!");
                continue;
            }
            if (status.Contains("Hacked", StringComparer.OrdinalIgnoreCase) && settings?.OfficialOnly == true)
            {
                yield return new ErrorMessage($"\"{title}\" is hacked!");
                continue;
            }

            
            _ = DateTime.TryParse(game.ReleaseDate ?? "", CultureInfo.InvariantCulture, DateTimeStyles.None, out var releaseDate);
            _ = DateTime.TryParse(game.LastPlayed ?? "", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastPlayed);
            var strPath = game.ApplicationPath ?? "";
            AbsolutePath path = default;
            if (!string.IsNullOrEmpty(strPath))
            {
                Path.Combine(GetBasePath() ?? "", game.ApplicationPath ?? "");
                path = Path.IsPathFullyQualified(strPath) ? _fileSystem.FromUnsanitizedFullPath(strPath) : default;
            }
            var cmd = game.LaunchCommand ?? "";
            GetPicPaths(strId, out var logo, out var shot);
            var isInstalled = false;
            if (dataPacks is not null && dataPacks.TryGetValue(strId, out var pack))
            {
                if (path == default)
                {
                    strPath = pack.path;
                    if (!string.IsNullOrEmpty(strPath))
                    {
                        Path.Combine(GetBasePath() ?? "", strPath);
                        path = Path.IsPathFullyQualified(strPath) ? _fileSystem.FromUnsanitizedFullPath(strPath) : default;
                    }
                    cmd = pack.cmd;
                }
                isInstalled = pack.installed;
            }
            

            yield return new FlashpointGame(
                Id: id,
                Title: title,
                GamePath: GetGamePath(strId),
                ApplicationPath: path,
                LaunchCommand: cmd,
                LaunchUrl: FlashpointUrl + strId,
                Playtime: TimeSpan.FromSeconds(game.Playtime ?? 0),
                IsInstalled: isInstalled,
                OriginalDescription: game.OriginalDescription ?? "",
                Developer: game.Developer ?? "",
                Publisher: game.Publisher ?? "",
                Series: game.Series ?? "",
                PlayModes: [.. (game.PlayMode ?? "").Split("; ")], // "Single Player", "Multiplayer", and/or "Cooperative"
                Version: game.Version ?? "",
                Tags: tags,
                Platforms: [.. (game.PlatformsStr ?? "").Split("; ")],  // "Flash", "HTML5", "Java", "Shockwave", "Silverlight", etc.
                Logo: logo,
                Screenshot: shot,
                Notes: game.Notes ?? "");
        }
    }

    /*
    private Dictionary<int, string?> GetPlatformAliases(AbsolutePath database)
    {
        Dictionary<int, string?> platforms = [];
        var aliases = SQLiteHelpers.GetDataTable(database, "SELECT * FROM platform_alias;").ToList<PlatformAlias>();
        if (aliases is null)
            return [];

        foreach (var alias in aliases)
        {
            if (alias.PlatformId is not null)
                platforms.Add((int)alias.PlatformId, alias.Name);
        }

        return platforms;
    }
    */

    private static Dictionary<string, (bool installed, string? path, string? cmd)> GetDataPacks(AbsolutePath database)
    {
        Dictionary<string, (bool, string?, string?)> data = [];
        var dataPacks = SQLiteHelpers.GetDataTable(database, "SELECT * FROM game_data;").ToList<DataPacks>();
        if (dataPacks is null)
            return [];

        foreach (var datum in dataPacks)
        {
            var id = datum.GameId ?? "";
            if (!string.IsNullOrEmpty(id))
                data.Add(datum.GameId ?? "", new(datum.PresentOnDisk == true, datum.ApplicationPath, datum.LaunchCommand));
        }

        return data;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private List<string> GetExtremeTags()
    {
        List<string> tags = [];

        try
        {
            using var stream = GetPrefsFilePath().Read();
            if (stream.Length == 0)
                return [];

            var prefs = JsonSerializer.Deserialize<Preferences>(stream, JsonSerializerOptions);
            if (prefs is null || prefs.TagFilters is null)
                return [];
            foreach (var filter in prefs.TagFilters)
            {
                if (filter.Enabled == true && filter.Tags is not null)
                {
                    tags.AddRange(filter.Tags);
                }
            }
        }
        catch (Exception) { }

        return tags;
    }

    internal string? GetBasePath()
    {
        var exePath = FindClient().GetFullPath();
        if (exePath is not null && Path.IsPathFullyQualified(exePath))
            return Path.GetDirectoryName(exePath);

        return null;
    }

    internal AbsolutePath GetGamePath(string id)
    {
        var path = GetBasePath();
        if (!string.IsNullOrEmpty(path))
            return _fileSystem.FromUnsanitizedFullPath(Path.Combine(path, "Data", "Games", id + ".zip"));

        return default;
    }

    internal void GetPicPaths(string id, out AbsolutePath logo, out string? shot)
    {
        logo = default;
        shot = null;
        var path = GetBasePath();
        if (!string.IsNullOrEmpty(path))
        {
            logo = _fileSystem.FromUnsanitizedFullPath(Path.Combine(path, "Data", "Images", "Logos", id[0..1], id[2..3], id));
            shot = Path.Combine(path, "Data", "Images", "Screenshots", id[0..1], id[2..3], id);
        }
    }

    internal AbsolutePath GetDatabaseFilePath()
    {
        var path = GetBasePath();
        if (!string.IsNullOrEmpty(path))
            return _fileSystem.FromUnsanitizedFullPath(Path.Combine(path, "Data", "flashpoint.sqlite"));

        return default;
    }

    internal AbsolutePath GetPrefsFilePath()
    {
        var path = GetBasePath();
        if (!string.IsNullOrEmpty(path))
            return _fileSystem.FromUnsanitizedFullPath(Path.Combine(path, "preferences.json"));

        return default;
    }
}
