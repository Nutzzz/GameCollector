using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameCollector.SQLiteUtils;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.GOG;

public partial class GOGHandler : AHandler<GOGGame, GOGGameId>
{
    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = SourceGenerationContext.Default,
        };

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    internal IDictionary<GOGGameId, OneOf<GOGGame, ErrorMessage>> FindGamesFromDatabase(Settings? settings = null)
    {
        /*
        productId from ProductAuthorizations
        productId, installationPath from InstalledBaseProducts
        productId, images, title from LimitedDetails
        //images = icon from json
        id, gameReleaseKey from PlayTasks
        playTaskId, executablePath, commandLineArgs from PlayTaskLaunchParameters
        limitedDetailsId, releaseDate from Details
        */

        Dictionary<GOGGameId, OneOf<GOGGame, ErrorMessage>> games = [];
        var database = GetDatabaseFile(_fileSystem);

        if (!database.FileExists)
        {
            _ = games.TryAdd(default, new ErrorMessage("GOG database not found."));
            return games;
        }

        var launcherPath = "";

        var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using (var key = localMachine32.OpenSubKey(@"SOFTWARE\GOG.com\GalaxyClient\paths"))
        {
            if (key != null && key.TryGetString("client", out launcherPath))
            {
                launcherPath = Path.Combine(launcherPath, "GalaxyClient.exe");
                if (!File.Exists(launcherPath))
                    launcherPath = "";
            }
        }

        var i = 100;
        try
        {
            // Get installed, owned not-installed, and unowned games

            var ltdDetails = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM LimitedDetails;").ToList<LimitedDetails>();
            var userRelProps = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM UserReleaseProperties;").ToList<UserReleaseProperties>();
            var userRelTags = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM UserReleaseTags;").ToList<UserReleaseTags>();
            var prodsToRelKeys = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM ProductsToReleaseKeys;").ToList<ProductsToReleaseKeys>();
            var gamePieces = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM GamePieces;").ToList<GamePieces>();
            var builds = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM Builds;").ToList<Builds>();
            var instBaseProds = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM InstalledBaseProducts;").ToList<InstalledBaseProducts>();
            var playTasks = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM PlayTasks;").ToList<PlayTasks>();
            var playTaskParams = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM PlayTaskLaunchParameters;").ToList<PlayTaskLaunchParameters>();
            var lastPlayed = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM LastPlayedDates;").ToList<LastPlayedDates>();
            var details = SQLiteHelpers.GetDataTable(database,
                "SELECT * FROM Details;").ToList<Details>();

            if (ltdDetails is null || builds is null)
            {
                i++;
                _ = games.TryAdd(GOGGameId.From(i), new ErrorMessage("Malformed GOG database output!"));
                return games;
            }
            foreach (var game in ltdDetails)
            {
                i++;

                if (game.ProductId is null)
                    continue;

                var sId = game.ProductId;
                GOGGameId id;
                if (long.TryParse(sId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iId))
                    id = GOGGameId.From(iId);
                else
                    continue;
                var key = string.IsNullOrEmpty(sId) ? "" : $"gog_{sId}";
                var name = game.Title;
                var imageUrl = "";
                var imageWideUrl = "";
                var iconUrl = "";
                var storeUrl = "";
                var supportUrl = "";
                var isHidden = false;
                List<string> tags = [];

                if (!string.IsNullOrEmpty(game.Links))
                {
                    var links = JsonSerializer.Deserialize<LinksJson>(game.Links, JsonSerializerOptions);
                    imageWideUrl = links?.Logo?.Href;
                    imageUrl = links?.BoxArtImage?.Href;
                    iconUrl = links?.IconSquare?.Href ?? links?.Icon?.Href;
                    storeUrl = links?.Store?.Href;
                    supportUrl = links?.Support?.Href ?? links?.Forum?.Href ?? links?.Store?.Href;
                }
                if (string.IsNullOrEmpty(imageWideUrl))
                {
                    if (!string.IsNullOrEmpty(game.Images))
                    {
                        var images = JsonSerializer.Deserialize<ImagesJson>(game.Images, JsonSerializerOptions);
                        imageWideUrl = images?.Logo2x;
                    }
                }

                // grab hidden status
                if (userRelProps is not null)
                {
                    foreach (var prop in userRelProps.Where(p => string.Equals(p.ReleaseKey, key, StringComparison.Ordinal)))
                    {
                        if (ushort.TryParse(prop.IsHidden, out var iHidden) && iHidden == 1)
                            isHidden = true;
                        break;
                    }
                }

                // grab user tags (can be multiple)
                if (userRelTags is not null)
                {
                    foreach (var tag in userRelTags.Where(t => string.Equals(t.ReleaseKey, key, StringComparison.Ordinal)))
                    {
                        if (!string.IsNullOrEmpty(tag.Tag))
                            tags.Add(tag.Tag);
                    }
                }

                if (prodsToRelKeys is not null)
                {
                    foreach (var relKey in prodsToRelKeys.Where(k =>
                        !string.IsNullOrEmpty(k.GogId) &&
                        k.GogId.Equals(sId, StringComparison.Ordinal)))
                    {
                        key = relKey.ReleaseKey;
                        break;
                    }
                }

                GOGGameId? parentId = null;
                var isDlc = false;
                ushort? myRating = null;

                // grab the DLC parent or user's rating
                if (gamePieces is not null)
                {
                    foreach (var piece in gamePieces.Where(p => string.Equals(p.ReleaseKey, key, StringComparison.Ordinal)))
                    {
                        if (!string.IsNullOrEmpty(piece.Value))
                        {
                            var value = JsonSerializer.Deserialize<ValueJson>(piece.Value, JsonSerializerOptions);
                            var parent = value?.ParentGrk;
                            if (!string.IsNullOrEmpty(parent))
                            {
                                if (settings?.BaseOnly == true)
                                {
                                    isDlc = true;
                                }
                                if (parent.StartsWith("gog_", StringComparison.Ordinal))
                                {
                                    parent = parent[4..];
                                    if (long.TryParse(parent, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iParent))
                                    {
                                        parentId = GOGGameId.From(iParent);
                                    }
                                }
                            }

                            var sRating = value?.Rating;
                            if (string.IsNullOrEmpty(sRating))
                            {
                                if (ushort.TryParse(sRating, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iRating))
                                    myRating = iRating;
                            }
                        }
                    }
                }
                if (isDlc) // Only flagged if DLC is not allowed
                {
                    i++;
                    _ = games.TryAdd(GOGGameId.From(i), new ErrorMessage($"\"{name}\" is a DLC"));
                    continue;
                }

                // Ensure this is an owned GOG game
                if (!builds.Any(b => !string.IsNullOrEmpty(b.ProductId) && b.ProductId.Equals(sId, StringComparison.Ordinal)))
                {
                    if (settings?.OwnedOnly == true)
                        continue;

                    // Add unowned games
                    _ = games.TryAdd(id, new GOGGame(
                        Id: id,
                        Name: name ?? (key ?? ""),
                        Path: new(),
                        BuildId: 0,
                        LaunchUrl: $"goggalaxy://openGameView/{sId}",
                        IsInstalled: false,
                        IsOwned: false,
                        IsHidden: isHidden,
                        Tags: tags,
                        MyRating: myRating,
                        ParentId: parentId,
                        StoreUrl: storeUrl ?? "",
                        SupportUrl: supportUrl ?? "",
                        BoxArtUrl: imageUrl ?? "",
                        LogoUrl: imageWideUrl ?? "",
                        IconUrl: iconUrl ?? ""));

                    continue;
                }

                var launch = "";
                var launchParam = "";
                var exe = "";
                string? path = null;
                ulong buildId = 0;
                var installDate = DateTime.MinValue;
                var lastRun = DateTime.MinValue;
                var releaseDate = DateTime.MinValue;

                if (instBaseProds is not null)
                {
                    // can be multiple
                    foreach (var prod in instBaseProds.Where(p =>
                        !string.IsNullOrEmpty(p.ProductId) &&
                        p.ProductId.Equals(sId, StringComparison.Ordinal)))
                    {
                        path = prod.InstallationPath;
                        _ = ulong.TryParse(prod.BuildId, out buildId);

                        DateTime.TryParseExact(
                            prod.InstallationDate,
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal,
                            out installDate);
                        break;
                    }
                    if (path is null)
                    {
                        // Add not-installed games
                        if (settings?.InstalledOnly != true)
                        {
                            _ = games.TryAdd(id, new GOGGame(
                                Id: id,
                                Name: name ?? (key ?? ""),
                                Path: new(),
                                BuildId: buildId,
                                LaunchUrl: $"goggalaxy://openGameView/{sId}",
                                IsInstalled: false,
                                IsOwned: true,
                                IsHidden: isHidden,
                                Tags: tags,
                                MyRating: myRating,
                                ParentId: parentId,
                                BoxArtUrl: imageUrl ?? "",
                                LogoUrl: imageWideUrl ?? "",
                                IconUrl: iconUrl ?? ""));
                            continue;
                        }
                    }
                }

                if (playTasks is not null && playTaskParams is not null)
                {
                    foreach (var task in playTasks.Where(t => string.Equals(t.GameReleaseKey, key, StringComparison.Ordinal)))
                    {
                        var taskId = task.Id;
                        foreach (var par in playTaskParams.Where(p =>
                            !string.IsNullOrEmpty(p.PlayTaskId) &&
                            p.PlayTaskId.Equals(taskId, StringComparison.Ordinal)))
                        {
                            exe = par.ExecutablePath;
                            if (string.IsNullOrEmpty(launcherPath))
                            {
                                launch = exe;
                                launch = $"\"{launch}\" {par.CommandLineArgs}";
                            }
                            else
                            {
                                launch = launcherPath;
                                launchParam = $"/command=runGame /gameId={sId} /path=\"" + Path.GetDirectoryName(exe) + "\"";
                                if (launch?.Length + launchParam.Length > 8190)
                                    launchParam = $"/command=runGame /gameId={sId}";
                            }
                        }
                        break;
                    }
                }

                // grab last run date
                if (lastPlayed is not null)
                {
                    foreach (var last in lastPlayed.Where(l => string.Equals(l.GameReleaseKey, key, StringComparison.Ordinal)))
                    {
                        if (DateTime.TryParseExact(last.LastPlayedDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var tmpLastRun))
                            lastRun = tmpLastRun;
                        break;
                    }
                }

                // Details table only applies to some installed GOG games
                if (details is not null)
                {
                    foreach (var detail in details.Where(d => d.Equals(iId)))
                    {
                        _ = DateTime.TryParseExact(
                            detail.ReleaseDate,
                            "yyyy-MM-dd'T'HH:mm:sszz00'",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal,
                            out releaseDate);
                        break;
                    }
                }

                AbsolutePath launchPath = new();
                AbsolutePath exePath = new();
                AbsolutePath installPath = new();
                if (!string.IsNullOrEmpty(launch) && Path.IsPathFullyQualified(launch))
                    launchPath = _fileSystem.FromUnsanitizedFullPath(launch);
                if (!string.IsNullOrEmpty(path) && Path.IsPathFullyQualified(path))
                    installPath = _fileSystem.FromUnsanitizedFullPath(path);
                if (!string.IsNullOrEmpty(exe) && Path.IsPathFullyQualified(exe))
                {
                    exePath = _fileSystem.FromUnsanitizedFullPath(exe);
                    if (installPath == default && !string.IsNullOrEmpty(exePath.Directory))
                        installPath = _fileSystem.FromUnsanitizedFullPath(exePath.Directory);
                }

                // Add installed games
                _ = games.TryAdd(id, new GOGGame(
                    Id: id,
                    Name: name ?? (key ?? ""),
                    Path: installPath,
                    BuildId: buildId,
                    Launch: launchPath,
                    LaunchParam: launchParam,
                    Exe: exePath,
                    InstallDate: installDate,
                    LastPlayedDate: lastRun,
                    IsInstalled: true,
                    IsOwned: true,
                    IsHidden: isHidden,
                    Tags: tags,
                    MyRating: myRating,
                    ParentId: parentId,
                    ReleaseDate: releaseDate,
                    BoxArtUrl: imageUrl ?? "",
                    LogoUrl: imageWideUrl ?? "",
                    IconUrl: iconUrl ?? ""));
            }

            return games;
        }
        catch (Exception e)
        {
            i++;
            _ = games.TryAdd(GOGGameId.From(i), new ErrorMessage(e, "Malformed GOG database output!"));
            return games;
        }
    }

    internal static AbsolutePath GetDatabaseFile(IFileSystem fileSystem)
    {
        return fileSystem.GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("GOG.com")
            .Combine("Galaxy")
            .Combine("storage")
            .Combine("galaxy-2.0.db");
    }
}
