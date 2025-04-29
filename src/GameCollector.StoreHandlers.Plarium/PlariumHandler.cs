using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using System.Globalization;

namespace GameCollector.StoreHandlers.Plarium;

/// <summary>
/// Handler for finding games installed with Plarium Play.
/// </summary>
/// <remarks>
/// Constructor. Uses json file:
///   %LocalAppData%\PlariumPlay\gamestorage.gsfn
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
public class PlariumHandler(IFileSystem fileSystem, IRegistry? registry = null) : AHandler<PlariumGame, PlariumGameId>
{
    internal const string PlariumRegKey = @"Software\PlariumPlayInstaller";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
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
    public override Func<PlariumGame, PlariumGameId> IdSelector => game => game.ProductId;

    /// <inheritdoc/>
    public override IEqualityComparer<PlariumGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(PlariumRegKey);
            if (regKey is not null)
            {
                if (regKey.TryGetString("InstallFolder", out var folder) && Path.IsPathRooted(folder))
                {
                    var play = _fileSystem.FromUnsanitizedFullPath(folder).Combine("PlariumPlay.exe");
                    if (play.FileExists) return play;  // NB: The registry isn't always right
                }
            }
        }

        return GetPlariumPlayPath().Combine("PlariumPlay.exe");
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
    Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override IEnumerable<OneOf<PlariumGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var jsonFile = GetPlariumPlayPath().Combine("gamestorage.gsfn");
        if (!jsonFile.FileExists)
        {
            yield return new ErrorMessage($"The file {jsonFile.GetFullPath()} does not exist!");
            yield break;
        }

        using var stream = jsonFile.Read();
        var gameStorage = JsonSerializer.Deserialize<GameStorage>(stream, JsonSerializerOptions);
        if (gameStorage is null)
        {
            yield return new ErrorMessage($"Unable to deserialize file {jsonFile.GetFullPath()}");
            yield break;
        }
        var ti = new CultureInfo("en-US", useUserOverride: false).TextInfo;
        foreach (var game in gameStorage.InstalledGames)
        {
            ulong id = 0;
            var gameId = "";
            var gameName = "";
            var name = "";
            var strPath = "";
            AbsolutePath path = new();
            AbsolutePath gamePath = new();
            AbsolutePath exe = new();
            var args = "";
            var company = "";

            id = game.Value.Id ?? 0;
            if (id == 0)
                continue;

            var games = game.Value.InsalledGames; // [sic]
            if (games is not null && games.Count > 0)
            {
                gameName = games.Keys.ToArray()[0];
                gameId = games.Values.ToArray()[0];
            }
            else continue;

            strPath = game.Value.InstallationPath ?? "";
            if (string.IsNullOrEmpty(strPath) || strPath.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                exe = GetPlariumPlayPath().Combine("PlariumPlay.exe");
                args = $"-gameid={id} -tray-start";
            }
            else
            {
                path = Path.IsPathRooted(strPath) ? _fileSystem.FromUnsanitizedFullPath(strPath) : new();
                if (!string.IsNullOrEmpty(gameName) && !string.IsNullOrEmpty(gameId))
                {
                    gamePath = path.Combine(gameName).Combine(gameId);
                    if (gamePath.DirectoryExists())
                    {
                        exe = Utils.FindExe(gamePath, _fileSystem, gameName);
                        var settingsFile = gamePath.Combine("settings.json");
                        var appInfoFile = gamePath.Combine($"{gameName}_Data").Combine("app.info");
                        if (settingsFile.FileExists)
                        {
                            using var settingsStream = settingsFile.Read();
                            var gameSettings = JsonSerializer.Deserialize<GameSettings>(settingsStream);
                            if (gameSettings is not null)
                            {
                                company = gameSettings.CompanyName;
                                name = gameSettings.ProductName;
                            }
                        }
                        if (string.IsNullOrEmpty(name) && appInfoFile.FileExists)
                        {
                            using var appInfoStream = appInfoFile.Read();
                            using var reader = new StreamReader(appInfoStream);
                            var line = reader.ReadLine();
                            if (line is not null)
                            {
                                company = line;
                                line = reader.ReadLine();
                                if (line is not null)
                                    name = line;
                            }
                        }
                    }
                }
            }
            yield return new PlariumGame(
                ProductId: PlariumGameId.From(id),
                ProductName: string.IsNullOrEmpty(name) ? ti.ToTitleCase(gameName) ?? id.ToString() : name,
                InstallationPath: gamePath == default ? path : gamePath,
                Launch: exe,
                LaunchArgs: args,
                GameId: gameId,
                GameName: gameName,
                CompanyName: company);
        }
    }

    public AbsolutePath GetPlariumPlayPath()
    {
        // The path changed slightly between versions, and the registry isn't always right, so we'll look in both places
        var path = _fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .Combine("PlariumPlay");
        if (!path.DirectoryExists())
            path = _fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .Combine("Plarium")
            .Combine("PlariumPlay");
        return path;
    }
}
