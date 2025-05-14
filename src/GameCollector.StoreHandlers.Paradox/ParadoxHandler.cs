using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using GameCollector.StoreHandlers.Paradox;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameCollector.StoreHandlers.Paradox;

/// <summary>
/// Handler for finding games installed with Paradox Launcher.
/// </summary>
/// <remarks>
/// Constructor. Uses json files:
///   %AppData%\Paradox Interactive\launcher-v2\userSettings.json
///   %AppData%\Paradox Interactive\launcher-v2\game-metadata\game-metadata
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
public class ParadoxHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<ParadoxGame, ParadoxGameId>
{
    internal const string ParadoxRegKey = @"Software\Paradox Interactive\Paradox Launcher v2";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
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
    public override IEqualityComparer<ParadoxGameId>? IdEqualityComparer => ParadoxGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<ParadoxGame, ParadoxGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(ParadoxRegKey);
            if (regKey is not null)
            {
                if (regKey.TryGetString("LauncherInstallation", out var launcher) && Path.IsPathRooted(launcher))
                    return _fileSystem.FromUnsanitizedFullPath(launcher).Combine("bootstrapper-v2.exe");
            }
        }

        return default;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
    Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override IEnumerable<OneOf<ParadoxGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        List<OneOf<ParadoxGame, ErrorMessage>> games = [];

        var pdxPath = GetParadoxV2Path();
        var userFile = pdxPath.Combine("userSettings.json");
        Dictionary<string, string?> instPaths = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ulong?> runDates = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!userFile.FileExists)
                return [new ErrorMessage($"The file {userFile.GetFullPath()} does not exist!")];
 
            using var userStream = userFile.Read();
            if (userStream.Length == 0)
                return [new ErrorMessage($"File {userFile.GetFullPath()} is empty!")];
         
            var userSettings = JsonSerializer.Deserialize<UserSettings>(userStream, JsonSerializerOptions);
            if (userSettings is null)
                return [new ErrorMessage($"Unable to deserialize file {userFile.GetFullPath()}")];

            foreach (var libPath in userSettings.GameLibraryPaths)
            {
                if (libPath.ValueKind == JsonValueKind.String)
                    instPaths["default"] = libPath.ToString();
                else if (libPath.ValueKind == JsonValueKind.Object)
                {
                    if (libPath.TryGetProperty("gameId", out var id) &&
                        libPath.TryGetProperty("installationPath", out var path))

                        instPaths[id.ToString()] = path.ToString();
                }
            }
            var gamesLaunched = userSettings.GamesLaunched;
            foreach (var obj in gamesLaunched.EnumerateObject())
            {
                _ = ulong.TryParse(obj.Value.ToString(), out var date);
                runDates.Add(obj.Name, date > 0 ? date : null);
            }
        }
        catch (Exception e)
        {
            return [new ErrorMessage(e, $"Exception deserializing file {userFile.GetFullPath()}!")];
        }

        var metaFile = pdxPath.Combine("game-metadata").Combine("game-metadata");
        GameMetadata? metadata;
        try
        {
            if (!metaFile.FileExists)
                return [new ErrorMessage($"The file {metaFile.GetFullPath()} does not exist!")];

            using var metaStream = metaFile.Read();
            metadata = JsonSerializer.Deserialize<GameMetadata>(metaStream, JsonSerializerOptions);
        }
        catch (Exception e)
        {
            return [new ErrorMessage(e, $"Exception parsing {metaFile.GetFullPath()}!")];
        }

        if (metadata is not null && metadata.Data is not null && metadata.Data.Games is not null)
        {
            foreach (var game in metadata.Data.Games)
            {
                var id = game.Id;
                var name = game.Name ?? (game.Id?.Replace('_', ' ')) ?? "";
                var exe = Path.IsPathRooted(game.ExePath) ? _fileSystem.FromUnsanitizedFullPath(game.ExePath) : new();
                var args = game.ExeArgs;
                var strIcon = "";
                var strTaskIcon = "";
                var strBg = "";
                var strLogo = "";
                var strPath = "";
                ulong? lastLaunch = 0;
                if (game.ThemeSettings is not null)
                {
                    strIcon = game.ThemeSettings.AppIcon ?? "";
                    strTaskIcon = game.ThemeSettings.AppTaskbarIcon ?? "";
                    strBg = game.ThemeSettings.Background ?? "";
                    strLogo = game.ThemeSettings.Logo ?? "";
                }

                if (id is not null && instPaths.TryGetValue(id, out var instPath))
                {
                    strPath = instPath ?? "";
                    if (runDates.TryGetValue(id, out var runDate))
                        lastLaunch = runDate;
                }
                else if (instPaths.TryGetValue("default", out var instPathDefault))
                    strPath = instPathDefault ?? "";

                AbsolutePath path = new();
                if (!string.IsNullOrEmpty(strPath))
                {
                    if (Path.IsPathRooted(strPath))
                        path = _fileSystem.FromUnsanitizedFullPath(strPath);
                    else
                        path = GetParadoxV2Path().Combine(strPath);
                }
                AbsolutePath dataPath = new();
                if (path != default && path.DirectoryExists() && (exe == default || !exe.FileExists))
                {
                    var settingsFile = path.Combine("launcher-settings.json");
                    try
                    {
                        using var settingsStream = settingsFile.Read();
                        var launchSettings = JsonSerializer.Deserialize<LauncherSettings>(settingsStream, JsonSerializerOptions);
                        if (launchSettings is not null && launchSettings.ExePath is not null)
                        {
                            exe = path.Combine(launchSettings.ExePath);
                            if (launchSettings.GameDataPath is not null)
                            {
                                var strDataPath = launchSettings.GameDataPath.Replace("%USER_DOCUMENTS%",
                                    _fileSystem.GetKnownPath(KnownPath.MyDocumentsDirectory).GetFullPath(), StringComparison.Ordinal);
                                dataPath = _fileSystem.FromUnsanitizedFullPath(strDataPath);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        games.Add(new ErrorMessage(e, $"Exception parsing {settingsFile.GetFullPath()}!"));
                    }
                    if (exe == default || !exe.FileExists)
                        exe = Utils.FindExe(path, _fileSystem, name);

                    games.Add(new ParadoxGame(
                        Id: ParadoxGameId.From(id ?? ""),
                        Name: name,
                        InstallationPath: path,
                        GameDataPath: dataPath,
                        ExePath: exe,
                        ExeArgs: args,
                        AppIcon: Path.IsPathRooted(strIcon) ? _fileSystem.FromUnsanitizedFullPath(strIcon) : new(),
                        LastLaunch: lastLaunch,
                        NotFoundOnDisk: exe == default,
                        AppTaskbarIcon: Path.IsPathFullyQualified(strTaskIcon) ? _fileSystem.FromUnsanitizedFullPath(strTaskIcon) : new(),
                        Background: Path.IsPathFullyQualified(strBg) ? _fileSystem.FromUnsanitizedFullPath(strBg) : new(),
                        Logo: Path.IsPathFullyQualified(strLogo) ? _fileSystem.FromUnsanitizedFullPath(strLogo) : new()
                    ));
                }
            }
        }

        return games;
    }

    public AbsolutePath GetParadoxV1Path()
    {
        return _fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
        .Combine("Paradox Interactive")
        .Combine("launcher");
    }
    public AbsolutePath GetParadoxV2Path()
    {
        return _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
        .Combine("Paradox Interactive")
        .Combine("launcher-v2");
    }
}
