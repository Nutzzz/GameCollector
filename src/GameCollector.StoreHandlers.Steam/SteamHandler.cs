using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GameCollector.Common;
using GameCollector.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using ValveKeyValue;

namespace GameCollector.StoreHandlers.Steam;

/// <summary>
/// Handler for finding games installed with Steam.
/// </summary>
[PublicAPI]
public class SteamHandler : AHandler<Game, string>
{
    internal const string RegKey = @"Software\Valve\Steam";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry? _registry;
    private readonly IFileSystem _fileSystem;
    private readonly AbsolutePath _customSteamPath;

    private static readonly KVSerializerOptions KvSerializerOptions =
        new()
        {
            HasEscapeSequences = true,
            EnableValveNullByteBugBehavior = true,
        };

    /// <summary>
    /// Factory for Linux.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    [SupportedOSPlatform("linux")]
    public static SteamHandler CreateForLinux(IFileSystem fileSystem) =>
        new(fileSystem, registry: null);

    /// <summary>
    /// Factory for Windows that uses <see cref="WindowsRegistry"/>.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    [SupportedOSPlatform("windows")]
    public static SteamHandler CreateForWindows(IFileSystem fileSystem) =>
        new(fileSystem, new WindowsRegistry());

    /// <summary>
    /// Constructor. If you are on Windows, use <see cref="WindowsRegistry"/> for
    /// <paramref name="registry"/>. If you are on Linux, use <c>null</c> instead.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="registry"></param>
    public SteamHandler(IFileSystem fileSystem, IRegistry? registry)
    {
        _fileSystem = fileSystem;
        _registry = registry;
    }

    /// <summary>
    /// Constructor for specifying a custom Steam path. Only use this if this library can't
    /// find Steam using the default paths and registry. The custom path will be the only path
    /// this library looks at and should only be given if you known that Steam is located there.
    /// </summary>
    /// <param name="customSteamPath"></param>
    /// <param name="fileSystem"></param>
    /// <param name="registry"></param>
    public SteamHandler(AbsolutePath customSteamPath, IFileSystem fileSystem, IRegistry? registry)
    {
        _fileSystem = fileSystem;
        _registry = registry;
        _customSteamPath = customSteamPath;
    }

    /// <inheritdoc/>
    public override IEnumerable<Result<Game>> FindAllGames(bool installedOnly = false)
    {
        var (libraryFoldersFile, steamSearchError) = FindSteam();
        if (libraryFoldersFile == default)
        {
            yield return Result.FromError<Game>(steamSearchError ?? "Unable to find Steam!");
            yield break;
        }

        var libraryFolderPaths = ParseLibraryFoldersFile(libraryFoldersFile);
        if (libraryFolderPaths is null || libraryFolderPaths.Count == 0)
        {
            yield return Result.FromError<Game>($"Found no Steam Libraries in {libraryFoldersFile}");
            yield break;
        }

        foreach (var libraryFolderPath in libraryFolderPaths)
        {
            if (!_fileSystem.DirectoryExists(libraryFolderPath))
            {
                yield return Result.FromError<Game>($"Steam Library {libraryFolderPath} does not exist!");
                continue;
            }

            var acfFiles = _fileSystem
                .EnumerateFiles(libraryFolderPath, "*.acf", recursive: false)
                .ToArray();

            if (acfFiles.Length == 0)
            {
                yield return Result.FromError<Game>($"Library folder {libraryFolderPath} does not contain any manifests");
                continue;
            }

            foreach (var acfFile in acfFiles)
            {
                yield return ParseAppManifestFile(acfFile, libraryFolderPath);
            }
        }
    }

    /// <inheritdoc/>
    public override IDictionary<string, Game> FindAllGamesById(out string[] errors)
    {
        var (games, allErrors) = FindAllGames().SplitResults();
        errors = allErrors;

        return games.CustomToDictionary(game => game.Id, game => game);
    }

    private (AbsolutePath libraryFoldersFile, string? error) FindSteam()
    {
        if (_customSteamPath != default)
        {
            return (GetLibraryFoldersFile(_customSteamPath), null);
        }

        try
        {
            var defaultSteamDirs = GetDefaultSteamDirectories(_fileSystem)
                .ToArray();

            var libraryFoldersFile = defaultSteamDirs
                .Select(GetLibraryFoldersFile)
                .FirstOrDefault(file => _fileSystem.FileExists(file));

            if (libraryFoldersFile != default)
            {
                return (libraryFoldersFile, null);
            }

            if (_registry is null)
            {
                return (default, "Unable to find Steam in one of the default paths");
            }

            var steamDir = FindSteamInRegistry(_registry);
            if (steamDir == default)
            {
                return (default, "Unable to find Steam in the registry and one of the default paths");
            }

            if (!_fileSystem.DirectoryExists(steamDir))
            {
                return (default, $"Unable to find Steam in one of the default paths and the path from the registry does not exist: {steamDir}");
            }

            libraryFoldersFile = GetLibraryFoldersFile(steamDir);
            if (!_fileSystem.DirectoryExists(libraryFoldersFile))
            {
                return (default, $"Unable to find Steam in one of the default paths and the path from the registry is not a valid Steam installation because {libraryFoldersFile} does not exist");
            }

            return (libraryFoldersFile, null);
        }
        catch (Exception e)
        {
            return (default, $"Exception while searching for Steam:\n{e}");
        }
    }

    private AbsolutePath FindSteamInRegistry(IRegistry registry)
    {
        var currentUser = registry.OpenBaseKey(RegistryHive.CurrentUser);

        using var regKey = currentUser.OpenSubKey(RegKey);
        if (regKey is null) return default;

        if (!regKey.TryGetString("SteamPath", out var steamPath)) return default;

        var directoryInfo = _fileSystem.FromFullPath(steamPath);
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
                .Select(pathValue => _fileSystem.FromFullPath(pathValue))
                .Select(pathValue => pathValue.CombineUnchecked("steamapps"))
                .ToList();

            return paths.Any() ? paths : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Result<Game> ParseAppManifestFile(AbsolutePath manifestFile, AbsolutePath libraryFolder)
    {
        try
        {
            using var stream = _fileSystem.ReadFile(manifestFile);

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var data = kv.Deserialize(stream, KvSerializerOptions);

            if (!data.Name.Equals("AppState", StringComparison.OrdinalIgnoreCase))
            {
                return Result.FromError<Game>($"Manifest {manifestFile.GetFullPath()} is not a valid format!");
            }

            var appIdValue = data["appid"];
            if (appIdValue is null)
            {
                return Result.FromError<Game>($"Manifest {manifestFile.GetFullPath()} does not have the value \"appid\"");
            }

            var nameValue = data["name"];
            if (nameValue is null)
            {
                return Result.FromError<Game>($"Manifest {manifestFile.GetFullPath()} does not have the value \"name\"");
            }

            var installDirValue = data["installdir"];
            if (installDirValue is null)
            {
                return Result.FromError<Game>($"Manifest {manifestFile.GetFullPath()} does not have the value \"installdir\"");
            }

            var appId = appIdValue.ToInt32(NumberFormatInfo.InvariantInfo);
            var name = nameValue.ToString(CultureInfo.InvariantCulture);
            var installDir = installDirValue.ToString(CultureInfo.InvariantCulture);

            var gamePath = libraryFolder
                .CombineUnchecked("common")
                .CombineUnchecked(installDir);

            var id = appId.ToString(CultureInfo.InvariantCulture);
            var icon = "";
            if (registry is not null)
            {
                var localMachine64 = registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                var localMachine32 = registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var subKey64 = localMachine64.OpenSubKey(fileSystem.Path.Combine(UninstallRegKey, "Steam App " + id));
                using var subKey32 = localMachine32.OpenSubKey(fileSystem.Path.Combine(UninstallRegKey, "Steam App " + id));
                if (subKey64 is not null)
                {
                    icon = subKey64.GetString("DisplayIcon") ?? "";
                }
                if (string.IsNullOrEmpty(icon) && subKey32 is not null)
                {
                    icon = subKey32.GetString("DisplayIcon") ?? "";
                }
            }

            return Result.FromGame(new Game(
                Id: id,
                Name: name,
                Path: gamePath,
                Launch: "steam://rungameid/" + id,
                Icon: icon,
                Uninstall: "steam://uninstall/" + id));
        }
        catch (Exception e)
        {
            return Result.FromException<Game>($"Exception while parsing file {manifestFile.GetFullPath()}", e);
        }
    }
}
