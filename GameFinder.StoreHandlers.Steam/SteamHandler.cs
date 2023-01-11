using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using ValveKeyValue;
using Result = GameFinder.Common.Result<GameFinder.StoreHandlers.Steam.SteamGame>;

namespace GameFinder.StoreHandlers.Steam;

/// <summary>
/// Represents a game installed with Steam.
/// </summary>
/// <param name="AppId"></param>
/// <param name="Name"></param>
/// <param name="Path"></param>
[PublicAPI]
public record SteamGame(int AppId, string Name, string Path);

/// <summary>
/// Handler for finding games installed with Steam.
/// </summary>
[PublicAPI]
public class SteamHandler : AHandler<SteamGame, int>
{
    internal const string RegKey = @"Software\Valve\Steam";

    private readonly IRegistry? _registry;
    private readonly IFileSystem _fileSystem;
    private readonly string? _customSteamPath;

    /// <summary>
    /// Default constructor that uses the real filesystem <see cref="FileSystem"/> and
    /// the Windows registry <see cref="WindowsRegistry"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public SteamHandler() : this(new WindowsRegistry()) { }

    /// <summary>
    /// Constructor for specifying the registry. This uses the real filesystem <see cref="FileSystem"/>.
    /// If you are on Windows you should use <see cref="WindowsRegistry"/>, if you want to run tests
    /// use <see cref="InMemoryRegistry"/>.
    /// </summary>
    /// <param name="registry"></param>
    public SteamHandler(IRegistry? registry) : this(new FileSystem(), registry) { }

    /// <summary>
    /// Constructor for specifying the <see cref="IFileSystem"/> and <see cref="IRegistry"/> implementations.
    /// Use this constructor if you want to run tests.
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
    /// <exception cref="ArgumentException">The path <paramref name="customSteamPath"/> must be fully qualified.</exception>
    public SteamHandler(string customSteamPath, IFileSystem fileSystem, IRegistry? registry)
    {
        _fileSystem = fileSystem;
        _registry = registry;

        if (!_fileSystem.Path.IsPathFullyQualified(customSteamPath))
            throw new ArgumentException($"Path {customSteamPath} is not fully qualified!", nameof(customSteamPath));

        _customSteamPath = customSteamPath;
    }

    /// <inheritdoc/>
    public override IEnumerable<Result> FindAllGames()
    {
        var (libraryFoldersFile, steamSearchError) = FindSteam();
        if (libraryFoldersFile is null)
        {
            yield return new Result(null, steamSearchError ?? "Unable to find Steam!");
            yield break;
        }

        var libraryFolderPaths = ParseLibraryFoldersFile(libraryFoldersFile);
        if (libraryFolderPaths is null || libraryFolderPaths.Count == 0)
        {
            yield return new Result(null, $"Found no Steam Libraries in {libraryFoldersFile.FullName}");
            yield break;
        }

        foreach (var libraryFolderPath in libraryFolderPaths)
        {
            var libraryFolder = _fileSystem.DirectoryInfo.New(libraryFolderPath);
            if (!libraryFolder.Exists)
            {
                yield return new Result(null, $"Steam Library {libraryFolder.FullName} does not exist!");
                continue;
            }

            var acfFiles = libraryFolder
                .EnumerateFiles("*.acf", SearchOption.TopDirectoryOnly)
                .ToArray();

            if (acfFiles.Length == 0)
            {
                yield return new Result(null,$"Library folder {libraryFolder.FullName} does not contain any manifests");
                continue;
            }

            foreach (var acfFile in acfFiles)
            {
                yield return ParseAppManifestFile(acfFile, libraryFolder);
            }
        }
    }

    /// <inheritdoc/>
    public override Dictionary<int, SteamGame> FindAllGamesById(out string[] errors)
    {
        var (games, allErrors) = FindAllGames().SplitResults();
        errors = allErrors;

        return games.CustomToDictionary(game => game.AppId, game => game);
    }

    private (IFileInfo? libraryFoldersFile, string? error) FindSteam()
    {
        if (_customSteamPath is not null)
        {
            return (GetLibraryFoldersFile(_fileSystem.DirectoryInfo.New(_customSteamPath)), null);
        }

        try
        {
            var defaultSteamDirs = GetDefaultSteamDirectories(_fileSystem)
                .ToArray();

            var defaultSteamDirString = $"[{defaultSteamDirs.Select(dir => dir.FullName).Aggregate((a,b) => $"{a}, {b}")}]";

            var libraryFoldersFile = defaultSteamDirs
                .Select(GetLibraryFoldersFile)
                .FirstOrDefault(file => file.Exists);

            if (libraryFoldersFile is not null)
            {
                return (libraryFoldersFile, null);
            }

            if (_registry is null)
            {
                return (null, $"Unable to find Steam in one of the default paths {defaultSteamDirString}");
            }

            var steamDir = FindSteamInRegistry(_registry);
            if (steamDir is null)
            {
                return (null, $"Unable to find Steam in the registry and one of the default paths {defaultSteamDirString}");
            }

            if (!steamDir.Exists)
            {
                return (null, $"Unable to find Steam in one of the default paths {defaultSteamDirString} and the path from the registry does not exist: {steamDir.FullName}");
            }

            libraryFoldersFile = GetLibraryFoldersFile(steamDir);
            if (!libraryFoldersFile.Exists)
            {
                return (null, $"Unable to find Steam in one of the default paths {defaultSteamDirString} and the path from the registry is not a valid Steam installation because {libraryFoldersFile.FullName} does not exist");
            }

            return (libraryFoldersFile, null);
        }
        catch (Exception e)
        {
            return (null, $"Exception while searching for Steam:\n{e}");
        }
    }

    private IDirectoryInfo? FindSteamInRegistry(IRegistry registry)
    {
        var currentUser = registry.OpenBaseKey(RegistryHive.CurrentUser);

        using var regKey = currentUser.OpenSubKey(RegKey);
        if (regKey is null) return null;

        if (!regKey.TryGetString("SteamPath", out var steamPath)) return null;

        var directoryInfo = _fileSystem.DirectoryInfo.New(steamPath);
        return directoryInfo;
    }

    internal static IEnumerable<IDirectoryInfo> GetDefaultSteamDirectories(IFileSystem fileSystem)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam"
            ));

            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // steam on linux can be found in various places

            // ~/.local/share/Steam (common)
            // https://github.com/dotnet/runtime/blob/3b1df9396e2a7cc6797e76793e8547f8a7771953/src/libraries/System.Private.CoreLib/src/System/Environment.GetFolderPathCore.Unix.cs#L124
            yield return fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Steam"
            ));

            // ~/.steam (common)
            // https://github.com/dotnet/runtime/blob/3b1df9396e2a7cc6797e76793e8547f8a7771953/src/libraries/System.Private.CoreLib/src/System/Environment.GetFolderPathCore.Unix.cs#L90
            yield return fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".steam"
            ));

            // ~/.local/.steam (rare)
            yield return fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                ".steam"
            ));

            yield break;
        }

        throw new PlatformNotSupportedException();
    }

    internal static IFileInfo GetLibraryFoldersFile(IDirectoryInfo steamDirectory)
    {
        var fileSystem = steamDirectory.FileSystem;

        var fileInfo = fileSystem.FileInfo.New(fileSystem.Path.Combine(
            steamDirectory.FullName,
            "steamapps",
            "libraryfolders.vdf"));

        return fileInfo;
    }

    private List<string>? ParseLibraryFoldersFile(IFileInfo fileInfo)
    {
        try
        {
            using var stream = fileInfo.OpenRead();

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var data = kv.Deserialize(stream, new KVSerializerOptions
            {
                HasEscapeSequences = true,
                EnableValveNullByteBugBehavior = true
            });

            if (data is null) return null;
            if (!data.Name.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase)) return null;

            var paths = data.Children
                .Where(child => int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Select(child => child["path"])
                .Where(pathValue => pathValue is not null && pathValue.ValueType == KVValueType.String)
                .Select(pathValue => pathValue.ToString(CultureInfo.InvariantCulture))
                .Select(path => _fileSystem.Path.Combine(path, "steamapps"))
                .ToList();

            return paths.Any() ? paths : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Result ParseAppManifestFile(IFileInfo manifestFile, IDirectoryInfo libraryFolder)
    {
        try
        {
            using var stream = manifestFile.OpenRead();

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var data = kv.Deserialize(stream, new KVSerializerOptions
            {
                HasEscapeSequences = true,
                EnableValveNullByteBugBehavior = true
            });

            if (data is null)
            {
                return new Result(null, $"Unable to parse {manifestFile.FullName}");
            }

            if (!data.Name.Equals("AppState", StringComparison.OrdinalIgnoreCase))
            {
                return new Result(null, $"Manifest {manifestFile.FullName} is not a valid format!");
            }

            var appIdValue = data["appid"];
            if (appIdValue is null)
            {
                return new Result(null, $"Manifest {manifestFile.FullName} does not have the value \"appid\"");
            }

            var nameValue = data["name"];
            if (nameValue is null)
            {
                return new Result(null, $"Manifest {manifestFile.FullName} does not have the value \"name\"");
            }

            var installDirValue = data["installdir"];
            if (installDirValue is null)
            {
                return new Result(null, $"Manifest {manifestFile.FullName} does not have the value \"installdir\"");
            }

            var appId = appIdValue.ToInt32(NumberFormatInfo.InvariantInfo);
            var name = nameValue.ToString(CultureInfo.InvariantCulture);
            var installDir = installDirValue.ToString(CultureInfo.InvariantCulture);

            var gamePath = _fileSystem.Path.Combine(
                libraryFolder.FullName,
                "common",
                installDir
            );

            var game = new SteamGame(appId, name, gamePath);
            return new Result(game, null);
        }
        catch (Exception e)
        {
            return new Result(null, $"Exception while parsing file {manifestFile.FullName}:\n{e}");
        }
    }
}
