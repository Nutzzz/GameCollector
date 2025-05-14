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

namespace GameCollector.StoreHandlers.Itch;

/// <summary>
/// Handler for finding games installed with itch.
/// </summary>
/// <remarks>
/// Constructor. Uses SQLite database:
///   %AppData%\itch\db\butler.db
/// </remarks>
/// <param name="fileSystem">
/// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
/// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
/// a custom implementation or just a mock of the interface.
/// </param>
/// <param name="registry">
/// The implementation of <see cref="IRegistry"/> to use. For a shared instance
/// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <langword>null</langword>.
/// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
/// of the interface.
/// </param>
[PublicAPI]
public class ItchHandler(IFileSystem fileSystem, IRegistry? registry = null) : AHandler<ItchGame, ItchGameId>
{
    internal const string ItchLaunchUrl = "itch://games/";  // itch://games/<gameid>
    internal const string ItchStartUrl = "itch://caves/";   // itch://caves/<caveid>/launch
    internal const string ItchStartSuffix = "/launch";
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

    private readonly IRegistry? _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override Func<ItchGame, ItchGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<ItchGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        // could also get client path from HKEY_CLASSES_ROOT\itch\shell\open\command\(Default)
        // or just use protocol "itch://"

        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(Path.Combine(UninstallRegKey, "itch"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("DisplayVersion", out var ver) &&
                    regKey.TryGetString("InstallLocation", out var loc) &&
                    Path.IsPathRooted(loc))

                    return _fileSystem.FromUnsanitizedFullPath(loc).Combine($"app-{ver}");
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<ItchGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var database = GetDatabaseFilePath(_fileSystem);
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

    private IEnumerable<OneOf<ItchGame, ErrorMessage>> ParseDatabase(AbsolutePath database, Settings? settings = null)
    {
        var games = SQLiteHelpers.GetDataTable(database, "SELECT * FROM games;").ToList<ButlerGames>();
        var caves = SQLiteHelpers.GetDataTable(database, "SELECT * FROM caves;").ToList<ButlerCaves>();
        if (games is null)
        {
            yield return new ErrorMessage($"Could not deserialize file {database}");
            yield break;
        }

        foreach (var game in games)
        {
            var id = game.Id;
            if (string.IsNullOrEmpty(id))
            {
                yield return new ErrorMessage($"Value for \"id\" does not exist in table \"games\" in file {database}");
                continue;
            }
            var name = game.Title ?? "";
            var type = game.Classification ?? ""; // "game", "tool", or "assets"
            if (settings?.GamesOnly == true)
            {
                if (!type.Equals("game", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new ErrorMessage($"\"{name}\" is not a game (i.e., an asset or tool)!");
                    continue;
                }
            }

            DateTime? installDate = null;
            ulong runTime = 0;
            var isInstalled = false;

            var path = "";
            var launch = "";
            var url = "";

            if (caves is not null)
            {
                var result = ParseCavesForId([.. caves], id, name);
                if (result.IsError())
                {
                    if (settings?.InstalledOnly == true)
                    {
                        yield return result.AsError();
                        continue;
                    }
                    isInstalled = false;
                }
                else
                    (path, launch, url, installDate, runTime, isInstalled) = result.AsT0;
            }
            if (string.IsNullOrEmpty(url))
                url = ItchLaunchUrl + id;

            yield return new ItchGame(
                Id: ItchGameId.From(id),
                Title: name,
                Path: Path.IsPathRooted(path) ? _fileSystem.FromUnsanitizedFullPath(path) : new(),
                LaunchPath: Path.IsPathRooted(launch) ? _fileSystem.FromUnsanitizedFullPath(launch) : new(),
                OpenUrl: url,
                InstalledAt: installDate,
                SecondsRun: runTime,
                IsInstalled: isInstalled,
                ShortText: game.ShortText ?? "",
                Classification: type);
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private OneOf<(string path, string launch, string url, DateTime? installedAt, ulong secondsRun, bool isInstalled), ErrorMessage>
        ParseCavesForId(List<ButlerCaves> caves, string id, string name)
    {
        try
        {
            var path = "";
            var launch = "";
            var url = "";
            DateTime? installDate = null;
            ulong runTime = 0;
            var isInstalled = false;
            foreach (var install in caves)
            {
                if (id.Equals(install.GameId, StringComparison.OrdinalIgnoreCase))
                {
                    isInstalled = true;
                    //path = install.InstallFolderName;
                    if (install.Verdict is not null)
                    {
                        var verdict = JsonSerializer.Deserialize<Verdict>(install.Verdict, JsonSerializerOptions);
                        if (verdict is not null)
                        {
                            if (verdict.BasePath is not null &&
                                verdict.Candidates is not null)
                            {
                                path = verdict.BasePath;
                                foreach (var candidate in verdict.Candidates)
                                {
                                    if (candidate.Path is not null)
                                    {
                                        launch = @$"{path}\{candidate.Path}";
                                        url = ItchStartUrl + install.Id + ItchStartSuffix;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (install.InstalledAt is not null)
                        installDate = install.InstalledAt;
                    if (install.SecondsRun is not null)
                        runTime = (ulong)install.SecondsRun;
                    return (path, launch, url, installDate, runTime, isInstalled);
                }
            }
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing table \"caves\" for \"{name}\" [{id}]");
        }

        return new ErrorMessage($"\"{name}\" [{id}] not found in table \"caves\"");
    }

    internal static AbsolutePath GetDatabaseFilePath(IFileSystem fileSystem)
    {
        return fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .Combine("itch")
            .Combine("db")
            .Combine("butler.db");
    }
}
