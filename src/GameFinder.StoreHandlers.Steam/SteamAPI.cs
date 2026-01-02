using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.Steam.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusMods.Paths;
using OneOf;
using Steam.Models.SteamCommunity;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;
using ValveKeyValue;

namespace GameCollector.StoreHandlers.Steam;

public partial class SteamHandler : AHandler<SteamGame, Models.ValueTypes.AppId>
{
    private readonly string? _apiKey;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface.
    /// </param>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <c>null</c>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    /// <param name="logger">Logger.</param>
    /// <param name="apiKey"></param>
    public SteamHandler(IFileSystem fileSystem, IRegistry? registry = null, ILogger<SteamHandler>? logger = null, string? apiKey = null)
    {
        _fileSystem = fileSystem;
        _registry = registry;
        _logger = logger ?? NullLogger<SteamHandler>.Instance;
        _apiKey = apiKey;
    }

    internal IEnumerable<OneOf<SteamGame, ErrorMessage>> FindOwnedGamesFromAPI(
        Dictionary<Models.ValueTypes.AppId, OneOf<SteamGame, ErrorMessage>> installed,
        ulong userId = 0)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            yield return new ErrorMessage("Can't get Steam not-installed owned games. An API key must be provided. \n" +
                "To get a key, go to <https://steamcommunity.com/dev/apikey>.");
            foreach (var installedGame in installed)
            {
                if (installedGame.Value.IsT0)
                    yield return installedGame.Value.AsT0;
            }
            yield break;
        }

        if (userId < 1)
        {
            var userList = ParseLoginUsersFile();
            if (userList is not null)
            {
                userId = userList.Find(auto => auto.autoLogin).userId; // Get auto-login user
                if (userId < 1)
                    userId = userList.MaxBy(time => time.timeStamp).userId; // Get most recent user
            }
        }

        if (userId < 1)
        {
            yield return new ErrorMessage("Can't get Steam not-installed owned games. A Steam ID was not found. \n" +
                "To find your ID, go to <https://store.steampowered.com/account>.");
            foreach (var installedGame in installed)
            {
                if (installedGame.Value.IsT0)
                    yield return installedGame.Value.AsT0;
            }
            yield break;
        }

        var t = ParseAPIGames(userId);
        t.Wait();
        if (t.Result.IsError())
        {
            yield return t.Result.AsError();
            foreach (var installedGame in installed)
            {
                if (installedGame.Value.IsT0)
                    yield return installedGame.Value.AsT0;
            }
            yield break;
        }

        foreach (var owned in t.Result.AsT0.OwnedGames)
        {
            if (owned is null)
                continue;

            installed.TryGetValue((Models.ValueTypes.AppId)owned.AppId, out var installedGame);

            if (installedGame.IsT0 && installedGame.AsT0 is not null)
            {
                yield return new SteamGame(
                    installedGame.AsT0.SteamPath,
                    installedGame.AsT0.AppManifest,
                    installedGame.AsT0.RegistryEntry,
                    installedGame.AsT0.LibraryFolder,
                    OwnedGame: owned,
                    IsInstalled: true
                );
            }
            else
            {
                yield return new SteamGame(
                    SteamPath: default,
                    AppManifest: default,
                    RegistryEntry: default,
                    LibraryFolder: default,
                    OwnedGame: owned,
                    IsInstalled: false
                );
            }
        }
    }

    private static AbsolutePath GetAppDataFile(AbsolutePath steamDirectory)
    {
        return steamDirectory
            .Combine("config")
            .Combine("SteamAppData.vdf");
    }

    private static AbsolutePath GetLoginUsersFile(AbsolutePath steamDirectory)
    {
        return steamDirectory
            .Combine("config")
            .Combine("loginusers.vdf");
    }

    private List<(ulong userId, uint timeStamp, bool autoLogin)>? ParseLoginUsersFile()
    {
        List<(ulong, uint, bool)> userList = new();
        try
        {
            KVValue autoUser = "";

            if (!SteamLocationFinder.TryFindSteam(_fileSystem, _registry, _logger, out var steamPathResult))
            {
                return userList;
            }

            var steamPath = steamPathResult;
            var libraryFoldersFilePath = SteamLocationFinder.GetLibraryFoldersFilePath(steamPath);

            var libraryFoldersResult = LibraryFoldersManifestParser.ParseManifestFile(libraryFoldersFilePath);
            if (libraryFoldersResult.IsFailed)
            {
                return userList;
            }

            var libraryFolders = libraryFoldersResult.Value;

            var defaultSteamDirs = SteamLocationFinder.GetDefaultSteamInstallationPaths(_fileSystem)
                .ToArray();

            var appDataFile = defaultSteamDirs.Select(GetAppDataFile).FirstOrDefault(_fileSystem.FileExists);
            var loginUsersFile = defaultSteamDirs.Select(GetLoginUsersFile).FirstOrDefault(_fileSystem.FileExists);
            if (_registry is not null)
            {
                if (SteamLocationFinder.TryGetSteamPathFromRegistry(_fileSystem, _registry, _logger, out var steamDir))
                {
                    if (appDataFile == default)
                        appDataFile = GetAppDataFile(steamDir);
                    if (loginUsersFile == default)
                        loginUsersFile = GetLoginUsersFile(steamDir);
                }
            }

            using (var stream = _fileSystem.ReadFile(appDataFile))
            {
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                var data = kv.Deserialize(stream, KvSerializerOptions);

                if (data is null) return null;
                if (!data.Name.Equals("SteamAppData", StringComparison.OrdinalIgnoreCase)) return null;

                autoUser = data["AutoLoginUser"];
            }

            using (var stream = _fileSystem.ReadFile(loginUsersFile))
            {
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                var data = kv.Deserialize(stream, KvSerializerOptions);

                if (data is null) return null;
                if (!data.Name.Equals("users", StringComparison.OrdinalIgnoreCase)) return null;

                var users = data.Children.ToList();
                foreach (var user in users)
                {
                    if (!ulong.TryParse(user.Name, out var id))
                        continue;

                    var auto = false;
                    if (user.Value["AccountName"] is not null && (user.Value["AccountName"].ToString() ?? "")
                        .Equals(autoUser.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        auto = true;
                    }
                    _ = uint.TryParse(user.Value["Timestamp"].ToString(), out var time);

                    userList.Add((id, time, auto));
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return userList;
    }

    private async Task<OneOf<OwnedGamesResultModel, ErrorMessage>> ParseAPIGames(ulong userId)
    {
        try
        {
            SteamWebInterfaceFactory apiFactory = new(_apiKey);

            var userInterface = apiFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());
            var playerSummaryTask = userInterface.GetPlayerSummaryAsync(userId);
            playerSummaryTask.Wait();
            var userResponse = playerSummaryTask.Result;
            //DateTimeOffset? userLastModified = userResponse.LastModified;
            var userData = userResponse.Data;
            var visibility = userData.ProfileVisibility;
            //var profile = userData.ProfileUrl;

            if (visibility != ProfileVisibility.Public)
            {
                return new ErrorMessage("Can't get Steam not-installed owned games. Profile must be public. \n" +
                    "To change this, go to <https://steamcommunity.com/my/edit/settings>.");
            }

            var playerInterface = apiFactory.CreateSteamWebInterface<PlayerService>();
            var ownedGameTask = playerInterface.GetOwnedGamesAsync(
                userId,
                includeAppInfo: true,
                includeFreeGames: true);
            ownedGameTask.Wait();
            return ownedGameTask.Result.Data;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, "Exception looking for Steam owned games");
        }
    }
}
