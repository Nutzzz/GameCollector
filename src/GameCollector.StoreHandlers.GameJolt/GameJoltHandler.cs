using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.GameJolt;

/// <summary>
/// Handler for finding games installed with Game Jolt Client.
/// </summary>
/// <remarks>
/// Constructor. Uses JSON files:
///   %AppDataLocal%\game-jolt-client\User Data\Default\games.wttf
///   %AppDataLocal%\game-jolt-client\User Data\Default\packages.wttf
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
public class GameJoltHandler(IFileSystem fileSystem, IRegistry? registry = null) : AHandler<GameJoltGame, GameJoltGameId>
{
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

    /// <summary>
    /// The supported wttf version of this handler. You can change the version policy with
    /// <see cref="VersionPolicy"/>.
    /// </summary>
    public const int SupportedWttfVersion = 1;

    /// <summary>
    /// The supported manifest version of this handler. You can change the version policy with
    /// <see cref="VersionPolicy"/>.
    /// </summary>
    public const int SupportedManifestVersion = 3;

    /// <summary>
    /// Policy to use when the wttf or manifest version does not match <see cref="SupportedWttfVersion"/>
    /// or <see cref="SupportedManifestVersion"/>.
    /// The default behavior is <see cref="GameJolt.VersionPolicy.Warn"/>.
    /// </summary>
    public VersionPolicy VersionPolicy { get; set; } = VersionPolicy.Warn;

    /// <inheritdoc/>
    public override Func<GameJoltGame, GameJoltGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<GameJoltGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(Path.Combine(UninstallRegKey, "game-jolt-client_is1"));
            if (regKey is null) return default;

            if (regKey.TryGetString("InstallLocation", out var installDir) && Path.IsPathFullyQualified(installDir))
                return _fileSystem.FromUnsanitizedFullPath(installDir).Combine("GameJoltClient.exe");
        }

        return default;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code")]
    public override IEnumerable<OneOf<GameJoltGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        List<OneOf<GameJoltGame, ErrorMessage>> games = [];
        var filePath = _fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .Combine("game-jolt-client")
            .Combine("User Data")
            .Combine("Default");
        if (!filePath.DirectoryExists())
        {
            games.Add(new ErrorMessage($"{filePath.GetFullPath()} does not exist"));
            return games;
        }

        var packagesFile = filePath.Combine("packages.wttf");
        var gamesFile = filePath.Combine("games.wttf");
        try
        {
            using var stream = packagesFile.Read();
            var packageFile = JsonSerializer.Deserialize<PackagesFile>(stream, JsonSerializerOptions);
            using var stream2 = gamesFile.Read();
            var gameFile = JsonSerializer.Deserialize<GamesFile>(stream2, JsonSerializerOptions);

            if (packageFile is null || gameFile is null)
            {
                games.Add(new ErrorMessage($"packages.wttf or games.wttf in {filePath.GetFullPath()} cannot be read!"));
                return games;
            }

            var pkgFileVersionNullable = packageFile.Version;
            if (!pkgFileVersionNullable.HasValue)
            {
                games.Add(new ErrorMessage($"File {packagesFile.GetFullPath()} does not have a version!"));
                return games;
            }
            var gameFileVersionNullable = gameFile.Version;
            if (!gameFileVersionNullable.HasValue)
            {
                games.Add(new ErrorMessage($"File {gamesFile.GetFullPath()} does not have a version!"));
                return games;
            }

            var fileVersion = pkgFileVersionNullable.Value;
            var (versionMessage, isVersionError) = CreateVersionMessage(VersionPolicy, fileVersion, SupportedWttfVersion, packagesFile);
            if (versionMessage is not null)
            {
                games.Add(new ErrorMessage(versionMessage));
                if (isVersionError) return games;
            }
            fileVersion = gameFileVersionNullable.Value;
            (versionMessage, isVersionError) = CreateVersionMessage(VersionPolicy, fileVersion, SupportedWttfVersion, gamesFile);
            if (versionMessage is not null)
            {
                games.Add(new ErrorMessage(versionMessage));
                if (isVersionError) return games;
            }

            Dictionary<ulong, (string title, string dev, string imgUrl, string wideUrl)> gamesObjs = [];
            foreach (var obj in gameFile.Objects.EnumerateObject())
            {
                var game = JsonSerializer.Deserialize<Game>(obj.Value, JsonSerializerOptions);
                if (game is not null && game.Id is not null)
                {
                    var dev = "";
                    var imgUrl = "";
                    var wideUrl = "";
                    if (game.ThumbnailMediaItem is not null)
                        imgUrl = game.ThumbnailMediaItem.ImgUrl;
                    if (game.HeaderMediaItem is not null)
                        wideUrl = game.HeaderMediaItem.ImgUrl;
                    if (game.Developer is not null)
                        dev = game.Developer.DisplayName;

                    gamesObjs.Add((ulong)game.Id, new(game.Title ?? "", dev ?? "", imgUrl ?? "", wideUrl ?? ""));
                }
            }

            foreach (var obj in packageFile.Objects.EnumerateObject())
            {
                var package = JsonSerializer.Deserialize<Package>(obj.Value, JsonSerializerOptions);
                if (package is not null && package.GameId is not null)
                {
                    var id = (ulong)package.GameId;
                    var path = package.InstallDir;
                    AbsolutePath instDir = new();
                    AbsolutePath exePath = new();

                    if (!string.IsNullOrEmpty(path) && Path.IsPathFullyQualified(path))
                    {
                        instDir = _fileSystem.FromUnsanitizedFullPath(path);

                        var exe = "";
                        var manifestFile = instDir.Combine(".manifest");
                        if (manifestFile.FileExists)
                        {
                            using var stream3 = manifestFile.Read();
                            var manifest = JsonSerializer.Deserialize<ManifestFile>(stream3, JsonSerializerOptions);
                            if (manifest is null)
                                continue;
                            var manifestVersionNullable = manifest.Version;
                            if (!manifestVersionNullable.HasValue)
                            {
                                games.Add(new ErrorMessage($"File {manifestFile.GetFullPath()} does not have a version!"));
                                continue;
                            }
                            fileVersion = manifestVersionNullable.Value;
                            (versionMessage, isVersionError) = CreateVersionMessage(VersionPolicy, fileVersion, SupportedManifestVersion, manifestFile);
                            if (versionMessage is not null)
                            {
                                games.Add(new ErrorMessage(versionMessage));
                                if (isVersionError) continue;
                            }
                            if (manifest.GameInfo is not null && manifest.GameInfo.Dir is not null)
                                exePath = instDir.Combine(manifest.GameInfo.Dir);
                            if (manifest.LaunchOptions is not null)
                                exe = manifest.LaunchOptions.Executable;
                            if (!string.IsNullOrEmpty(exe))
                                exePath = exePath.Combine(exe);
                        }

                        if (string.IsNullOrEmpty(exe))
                        {
                            foreach (var opt in package.LaunchOptions)
                            {
                                if (opt.Os is not null && (opt.Os.Equals("windows_64", StringComparison.OrdinalIgnoreCase) ||
                                    (string.IsNullOrEmpty(exe) && opt.Os.Equals("windows", StringComparison.OrdinalIgnoreCase))))
                                {
                                    exe = opt.ExecutablePath;
                                }
                            }
                            if (!string.IsNullOrEmpty(exe))
                                exePath = instDir.Combine(exe);
                        }
                    }

                    var objExists = gamesObjs.TryGetValue(id, out var objs);
                    games.Add(new GameJoltGame(
                        Id: GameJoltGameId.From(id),
                        Title: objExists ? gamesObjs[id].title : exePath.GetFileNameWithoutExtension(),
                        InstallDir: instDir,
                        ExecutablePath: exePath,
                        Developer: objExists ? gamesObjs[id].dev : "",
                        ImageUrl: objExists ? gamesObjs[id].imgUrl : "",
                        HeaderUrl: objExists ? gamesObjs[id].wideUrl : "",
                        PackageId: package.Id ?? 0));
                }
            }
        }
        catch (Exception e)
        {
            games.Add(new ErrorMessage(e, "Exception while parsing wttf files"));
        }

        return games;
    }

    internal static (string? message, bool isError) CreateVersionMessage(
        VersionPolicy versionPolicy, int fileVersion, int supportedVersion, AbsolutePath filePath)
    {
        if (fileVersion == supportedVersion) return (null, false);

        return versionPolicy switch
        {
            VersionPolicy.Warn => (
                $"{filePath} has a file version " +
                $"{fileVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports version " +
                $"{supportedVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This message is a WARNING because the consumer of this library has set {nameof(VersionPolicy)} to {nameof(VersionPolicy.Warn)}",
                false),
            VersionPolicy.Error => (
                $"{filePath} has a file version " +
                $"{fileVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports version " +
                $"{supportedVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This is an ERROR because the consumer of this library has set {nameof(VersionPolicy)} to {nameof(VersionPolicy.Error)}",
                true),
            VersionPolicy.Ignore => (null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(versionPolicy), versionPolicy, message: null),
        };
    }
}
