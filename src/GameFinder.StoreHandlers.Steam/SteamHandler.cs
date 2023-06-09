using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using ValveKeyValue;

namespace GameFinder.StoreHandlers.Steam;

/// <summary>
/// Handler for finding games installed with Steam.
/// </summary>
[PublicAPI]
public partial class SteamHandler : AHandler<SteamGame, SteamGameId>
{
    internal const string RegKey = @"Software\Valve\Steam";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry? _registry;
    private readonly IFileSystem _fileSystem;

    private static readonly KVSerializerOptions KvSerializerOptions =
        new()
        {
            HasEscapeSequences = true,
            EnableValveNullByteBugBehavior = true,
        };

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
    public SteamHandler(IFileSystem fileSystem, IRegistry? registry)
    {
        _fileSystem = fileSystem;
        _registry = registry;
    }

    /// <inheritdoc/>
    public override Func<SteamGame, SteamGameId> IdSelector => game => game.AppId;

    /// <inheritdoc/>
    public override IEqualityComparer<SteamGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(RegKey);
            if (regKey is null) return default;

            if (regKey.TryGetString("SteamExe", out var steamExe) && Path.IsPathRooted(steamExe))
                return _fileSystem.FromFullPath(SanitizeInputPath(steamExe));
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<SteamGame, ErrorMessage>> FindAllGames(bool installedOnly = false, bool baseOnly = false)
    {
        return FindAllGames(installedOnly, baseOnly, 0);
    }

    /// <summary>
    /// Finds all Steam games
    /// </summary>
    /// <param name="installedOnly"></param>
    /// <param name="baseOnly"></param>
    /// <param name="userId"></param>
    public IEnumerable<OneOf<SteamGame, ErrorMessage>> FindAllGames(bool installedOnly = false, bool baseOnly = false, ulong userId = 0)
    {
        List<OneOf<SteamGame, ErrorMessage>> allGames = new();
        Dictionary<SteamGameId, OneOf<SteamGame, ErrorMessage>> installedGames = new();

        var steamSearchResult = FindSteam();
        if (steamSearchResult.TryGetError(out var error))
        {
            allGames.Add(error);
            return allGames;
        }

        var libraryFoldersFile = steamSearchResult.AsT0;

        var userDataPath = GetUserDataDirectory(libraryFoldersFile);
        var cloudSavesDirectories = CacheCloudSaveDirectories(userDataPath);

        var libraryFolderPaths = ParseLibraryFoldersFile(libraryFoldersFile);
        if (libraryFolderPaths is null || libraryFolderPaths.Count == 0)
        {
            allGames.Add(new ErrorMessage($"Found no Steam Libraries in {libraryFoldersFile}"));
            return allGames;
        }

        foreach (var libraryFolderPath in libraryFolderPaths)
        {
            if (!_fileSystem.DirectoryExists(libraryFolderPath))
            {
                installedGames.Add(SteamGameId.From(0), new ErrorMessage($"Steam Library {libraryFolderPath} does not exist!"));
                continue;
            }

            var acfFiles = _fileSystem
                .EnumerateFiles(libraryFolderPath, "*.acf", recursive: false)
                .ToArray();

            if (acfFiles.Length == 0)
            {
                installedGames.Add(SteamGameId.From(0), new ErrorMessage($"Library folder {libraryFolderPath} does not contain any manifests"));
                continue;
            }

            foreach (var acfFile in acfFiles)
            {
                var game = ParseAppManifestFile(acfFile, libraryFolderPath, cloudSavesDirectories);
                installedGames.Add(game.IsT0 ? game.AsT0.AppId : SteamGameId.From(0), game);
            }
        }

        if (installedOnly || _apiKey is null)
            return installedGames.Values;

        return FindOwnedGamesFromAPI(installedGames, userId);
    }
    
    private AbsolutePath GetUserDataDirectory(AbsolutePath libraryFoldersFile)
    {
        return libraryFoldersFile
            .Parent
            .Parent
            .CombineUnchecked("userdata");
    }

    private IReadOnlyDictionary<SteamGameId, AbsolutePath> CacheCloudSaveDirectories(AbsolutePath userDataDirectory)
    {
        if (!_fileSystem.DirectoryExists(userDataDirectory))
            return new Dictionary<SteamGameId, AbsolutePath>();

        var userDirectories = _fileSystem.EnumerateDirectories(userDataDirectory, recursive: false);

        var dictionary = userDirectories
            .Where(userDirectory => !string.Equals(userDirectory.FileName, "0", StringComparison.OrdinalIgnoreCase))
            .SelectMany(userDirectory => _fileSystem
                .EnumerateDirectories(userDirectory, recursive: false)
                .Select(cloudSaveDirectory => cloudSaveDirectory)
                .Select(cloudSaveDirectory =>
                {
                    var res = int.TryParse(cloudSaveDirectory.FileName, CultureInfo.InvariantCulture, out var gameId);
                    return (isValid: res, gameId, cloudSaveDirectory);
                })
                .Where(tuple => tuple.isValid)
                .Select(tuple => (gameId: SteamGameId.From(tuple.gameId), tuple.cloudSaveDirectory))
            )
            .GroupBy(x => x.gameId)
            .Select(x => x.First())
            .ToDictionary(x => x.gameId, x => x.cloudSaveDirectory);

        return dictionary;
    }

    private OneOf<AbsolutePath, ErrorMessage> FindSteam()
    {
        try
        {
            var defaultSteamDirs = GetDefaultSteamDirectories(_fileSystem)
                .ToArray();

            var libraryFoldersFile = defaultSteamDirs
                .Select(GetLibraryFoldersFile)
                .FirstOrDefault(file => _fileSystem.FileExists(file));

            if (libraryFoldersFile != default)
            {
                return libraryFoldersFile;
            }

            if (_registry is null)
            {
                return new ErrorMessage("Unable to find Steam in one of the default paths");
            }

            var steamDir = FindSteamInRegistry(_registry);
            if (steamDir == default)
            {
                return new ErrorMessage("Unable to find Steam in the registry and one of the default paths");
            }

            if (!_fileSystem.DirectoryExists(steamDir))
            {
                return new ErrorMessage($"Unable to find Steam in one of the default paths and the path from the registry does not exist: {steamDir}");
            }

            libraryFoldersFile = GetLibraryFoldersFile(steamDir);
            if (!_fileSystem.FileExists(libraryFoldersFile))
            {
                return new ErrorMessage($"Unable to find Steam in one of the default paths and the path from the registry is not a valid Steam installation because {libraryFoldersFile} does not exist");
            }

            return libraryFoldersFile;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, "Exception while searching for Steam");
        }
    }

    private AbsolutePath FindSteamInRegistry(IRegistry registry)
    {
        var currentUser = registry.OpenBaseKey(RegistryHive.CurrentUser);

        using var regKey = currentUser.OpenSubKey(RegKey);
        if (regKey is null) return default;

        if (!regKey.TryGetString("SteamPath", out var steamPath)) return default;

        var directoryInfo = _fileSystem.FromFullPath(SanitizeInputPath(steamPath));
        return directoryInfo;
    }

    [SuppressMessage("", "MA0051", Justification = "Deal with it.")]
    internal static IEnumerable<AbsolutePath> GetDefaultSteamDirectories(IFileSystem fileSystem)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return fileSystem
                .GetKnownPath(KnownPath.ProgramFilesX86Directory)
                .CombineUnchecked("Steam");

            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // steam on linux can be found in various places

            // $XDG_DATA_HOME/Steam aka ~/.local/share/Steam
            yield return fileSystem
                .GetKnownPath(KnownPath.LocalApplicationDataDirectory)
                .CombineUnchecked("Steam");

            // ~/.steam/debian-installation
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .CombineUnchecked(".steam")
                .CombineUnchecked("debian-installation");

            // ~/.var/app/com.valvesoftware.Steam/data/Steam (flatpak installation)
            // https://github.com/flatpak/flatpak/wiki/Filesystem
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .CombineUnchecked(".var/app/com.valvesoftware.Steam/data/Steam");

            // ~/.steam/steam
            // this is a legacy installation directory and is often soft linked to
            // the actual installation directory
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .CombineUnchecked(".steam")
                .CombineUnchecked("steam");

            // ~/.steam
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .CombineUnchecked(".steam");

            // ~/.local/.steam
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .CombineUnchecked(".local")
                .CombineUnchecked(".steam");

            yield break;
        }

        throw new PlatformNotSupportedException();
    }

    internal static AbsolutePath GetLibraryFoldersFile(AbsolutePath steamDirectory)
    {
        return steamDirectory
            .CombineUnchecked("steamapps")
            .CombineUnchecked("libraryfolders.vdf");
    }

    private List<AbsolutePath>? ParseLibraryFoldersFile(AbsolutePath path)
    {
        try
        {
            using var stream = _fileSystem.ReadFile(path);

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var data = kv.Deserialize(stream, KvSerializerOptions);

            if (data is null) return null;
            if (!data.Name.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase)) return null;

            var paths = data.Children
                .Where(child => int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Select(child => child["path"])
                .Where(pathValue => pathValue is not null && pathValue.ValueType == KVValueType.String)
                .Select(pathValue => pathValue.ToString(CultureInfo.InvariantCulture))
                .Select(pathValue => _fileSystem.FromFullPath(SanitizeInputPath(pathValue)))
                .Select(pathValue => pathValue.CombineUnchecked("steamapps"))
                .ToList();

            return paths.Any() ? paths : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private OneOf<SteamGame, ErrorMessage> ParseAppManifestFile(
        AbsolutePath manifestFile,
        AbsolutePath libraryFolder,
        IReadOnlyDictionary<SteamGameId, AbsolutePath> cloudSavesDirectories)
    {
        try
        {
            using var stream = _fileSystem.ReadFile(manifestFile);

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var data = kv.Deserialize(stream, KvSerializerOptions);

            if (!data.Name.Equals("AppState", StringComparison.OrdinalIgnoreCase))
            {
                return new ErrorMessage($"Manifest {manifestFile.GetFullPath()} is not a valid format!");
            }

            var appIdValue = data["appid"];
            if (appIdValue is null)
            {
                return new ErrorMessage($"Manifest {manifestFile.GetFullPath()} does not have the value \"appid\"");
            }

            var nameValue = data["name"];
            if (nameValue is null)
            {
                return new ErrorMessage($"Manifest {manifestFile.GetFullPath()} does not have the value \"name\"");
            }

            var installDirValue = data["installdir"];
            if (installDirValue is null)
            {
                return new ErrorMessage($"Manifest {manifestFile.GetFullPath()} does not have the value \"installdir\"");
            }

            var appId = appIdValue.ToInt32(NumberFormatInfo.InvariantInfo);
            var name = nameValue.ToString(CultureInfo.InvariantCulture);
            var installDir = installDirValue.ToString(CultureInfo.InvariantCulture);

            var gamePath = libraryFolder
                .CombineUnchecked("common")
                .CombineUnchecked(installDir);

            var icon = "";
            if (_registry is not null)
            {
                var localMachine64 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var subKey64 = localMachine64.OpenSubKey(Path.Combine(UninstallRegKey, "Steam App " + appId.ToString(CultureInfo.InvariantCulture)));
                using var subKey32 = localMachine32.OpenSubKey(Path.Combine(UninstallRegKey, "Steam App " + appId.ToString(CultureInfo.InvariantCulture)));
                if (subKey64 is not null)
                    icon = subKey64.GetString("DisplayIcon");
                if (string.IsNullOrEmpty(icon) && subKey32 is not null)
                    icon = subKey32.GetString("DisplayIcon");
            }

            var gameId = SteamGameId.From(appId);
            AbsolutePath? cloudSavesDirectory = cloudSavesDirectories.TryGetValue(gameId, out var tmp)
                ? tmp
                : null;

            var game = new SteamGame(
                AppId: gameId,
                Name: name,
                Path: gamePath,
                CloudSavesDirectory: cloudSavesDirectory,
                DisplayIcon: string.IsNullOrEmpty(icon) ? default : _fileSystem.FromFullPath(SanitizeInputPath(icon)));
            return game;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing file {manifestFile.GetFullPath()}");
        }
    }
}
