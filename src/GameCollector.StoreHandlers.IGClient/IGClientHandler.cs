using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.IGClient;

/// <summary>
/// Handler for finding games installed with Indiegala IGClient.
/// </summary>
/// <remarks>
/// Uses json files:
///   %AppData%\IGClient\storage\installed.json
///   %AppData%\IGClient\config.json
/// </remarks>
/// <remarks>
/// Constructor.
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
public class IGClientHandler(IFileSystem fileSystem, IRegistry? registry = null) : AHandler<IGClientGame, IGClientGameId>
{
    internal const string ImgUrl = "https://www.indiegalacdn.com/imgs/devs/";
    internal const string UninstallRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            //TODO: Fix TypeInfoResolver exception with ConfigFile
            //TypeInfoResolver = SourceGenerationContext.Default,
        };

    private readonly IRegistry? _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override IEqualityComparer<IGClientGameId>? IdEqualityComparer => IGClientGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<IGClientGame, IGClientGameId> IdSelector => game => game.IdKeyName;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine64 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            using var regKey = localMachine64.OpenSubKey(Path.Combine(UninstallRegKey, "6f4f090a-db12-53b6-ac44-9ecdb7703b4a"));
            if (regKey is null) return default;

            if (regKey.TryGetString("DisplayIcon", out var icon))
            {
                if (icon.Contains(',', StringComparison.Ordinal))
                    icon = icon[..icon.LastIndexOf(',')];
                if (Path.IsPathRooted(icon))
                    return _fileSystem.FromUnsanitizedFullPath(icon);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<IGClientGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        List<OneOf<IGClientGame, ErrorMessage>> games;
        var installFile = _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .Combine("IGClient")
            .Combine("storage")
            .Combine("installed.json");
        if (!installFile.FileExists)
        {
            return [new ErrorMessage($"The data file {installFile.GetFullPath()} does not exist!"),];
        }
        var configFile = _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .Combine("IGClient")
            .Combine("config.json");
        if (!configFile.FileExists)
        {
            return [new ErrorMessage($"The data file {configFile.GetFullPath()} does not exist!"),];
        }

        games = ParseInstallFile(installFile);
        if (settings?.InstalledOnly != true)
        {
            var ownedGames = ParseConfigFile(configFile, games);
            games.AddRange(ownedGames);
        }

        return games;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private List<OneOf<IGClientGame, ErrorMessage>> ParseInstallFile(AbsolutePath installFile)
    {
        List<OneOf<IGClientGame, ErrorMessage>> games = [];

        try
        {
            using var streamInst = installFile.Read();
            var installs = JsonSerializer.Deserialize<List<InstalledFile>>(streamInst, JsonSerializerOptions);
            if (installs is null || installs.Count == 0)
            {
                games.Add(new ErrorMessage($"Unable to deserialize data file {installFile.GetFullPath()}"));
                return games;
            }

            var i = 0;
            foreach (var game in installs)
            {
                i++;
                if (game.Path is null ||
                    game.Path.Count == 0)
                {
                    games.Add(new ErrorMessage("Unable to deserialize \"path\" property in data file " +
                        $"{installFile.GetFullPath()} for game #{i.ToString(CultureInfo.InvariantCulture)}"));
                    continue;
                }

                var id = "";
                var slugged = "";
                var name = "";
                AbsolutePath launch = new();
                var launchArgs = "";
                List<string> specs = [];
                var rating = 0m;

                AbsolutePath path = new();
                foreach (var gamePath in game.Path)
                {
                    if (Path.IsPathRooted(gamePath))
                        path = _fileSystem.FromUnsanitizedFullPath(gamePath);
                }
                var target = game.Target;
                if (target is null ||
                    target.ItemData is null ||
                    target.GameData is null)
                {
                    games.Add(new ErrorMessage("Unable to deserialize \"target\" property in data file " +
                        $"{installFile.GetFullPath()} for game #{i.ToString(CultureInfo.InvariantCulture)}"));
                    continue;
                }
                id = target.ItemData.IdKeyName ?? "";
                slugged = target.ItemData.SluggedName ?? "";
                path = path.Combine(slugged);
                name = target.ItemData.Name ?? "";

                if (!string.IsNullOrEmpty(target.GameData.ExePath))
                {
                    launch = path.Combine(target.GameData.ExePath);
                    if (!string.IsNullOrEmpty(target.GameData.Args))
                        launchArgs = target.GameData.Args;
                }
                else
                    launch = Utils.FindExe(path, _fileSystem, name);
                if (target.GameData.Rating is not null)
                    rating = target.GameData.Rating.AvgRating ?? 0m;
                games.Add(new IGClientGame(
                    IdKeyName: IGClientGameId.From(id),
                    ItemName: name,
                    Path: path,
                    ExePath: launch,
                    ExeArgs: launchArgs,
                    DescriptionShort: target.GameData.DescriptionShort ?? "",
                    DescriptionLong: target.GameData.DescriptionLong ?? "",
                    DevImage: $"{ImgUrl}{target.ItemData.DevId}/products/{id}/prodmain/{target.ItemData.DevImage}",
                    DevCover: $"{ImgUrl}{target.ItemData.DevId}/products/{id}/prodcover/{target.ItemData.DevCover}",
                    SluggedName: slugged,
                    Specs: target.GameData.Specs ?? [],
                    Categories: target.GameData.Categories,
                    Tags: target.GameData.Tags,
                    AvgRating: rating));
            }
            return games;
        }
        catch (Exception e)
        {
            return [new ErrorMessage(e, $"Exception while deserializing file {installFile.GetFullPath()}")];
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private List<OneOf<IGClientGame, ErrorMessage>> ParseConfigFile(AbsolutePath configFile, List<OneOf<IGClientGame, ErrorMessage>> games)
    {
        List<OneOf<IGClientGame, ErrorMessage>> ownedGames = [];
        try
        {
            using var streamCfg = configFile.Read();
            var allGames = JsonSerializer.Deserialize<Dictionary<string, ConfigFile>>(streamCfg, JsonSerializerOptions);
            if (allGames is null ||
                allGames.Count == 0 ||
                !allGames.TryGetValue("gala_data", out var config))
            {
                ownedGames.Add(new ErrorMessage($"Unable to deserialize data file {configFile.GetFullPath()}"));
                return ownedGames;
            }

            if (config.Data is null ||
                config.Data.ShowcaseContent is null ||
                config.Data.ShowcaseContent.Content is null ||
                config.Data.ShowcaseContent.Content.UserCollection is null)
            {
                ownedGames.Add(new ErrorMessage($"Unable to deserialize data file {configFile.GetFullPath()}"));
                return ownedGames;
            }
            var c = 0;
            var collection = config.Data.ShowcaseContent.Content.UserCollection ?? [];
            foreach (var game in collection)
            {
                c++;
                var installed = false;
                var id = game.ProdIdKeyName;
                var name = game.ProdName;
                if (string.IsNullOrEmpty(id))
                {
                    ownedGames.Add(new ErrorMessage("Unable to deserialize data in file " +
                        $"{configFile.GetFullPath()} for game #{c.ToString(CultureInfo.InvariantCulture)}"));
                    continue;
                }
                foreach (var installedGame in games)
                {
                    if (installedGame.IsGame() && installedGame.AsGame().GameId.Equals(id, StringComparison.Ordinal))
                    {
                        //ownedGames.Add(new ErrorMessage($"{name} was already found!"));
                        installed = true;
                        break;
                    }
                }
                if (installed) continue;

                var slugged = "";
                var description = "";
                var descriptionLong = "";
                List<string> specs = [];
                List<string> genres = [];
                List<string> tags = [];
                var rating = 0m;

                slugged = game.ProdSluggedName ?? "";
                if (string.IsNullOrEmpty(name))
                    name = slugged.Replace('-', ' ');

                if (allGames is not null &&
                    allGames.TryGetValue(slugged, out var gameData))
                {
                    description = gameData.DescriptionShort ?? "";
                    descriptionLong = gameData.DescriptionLong ?? "";
                    specs = gameData.Specs ?? [];
                    genres = gameData.Categories ?? [];
                    tags = gameData.Tags ?? [];
                    if (gameData.Rating is not null)
                        rating = gameData.Rating.AvgRating ?? 0m;
                }

                ownedGames.Add(new IGClientGame(
                    IdKeyName: IGClientGameId.From(id),
                    ItemName: name,
                    Path: new(),
                    IsInstalled: false,
                    DescriptionShort: description,
                    DescriptionLong: descriptionLong,
                    DevImage: $"{ImgUrl}{game.ProdDevNamespace}/products/{id}/prodmain/{game.ProdDevImage}",
                    DevCover: $"{ImgUrl}{game.ProdDevNamespace}/products/{id}/prodcover/{game.ProdDevCover}",
                    SluggedName: slugged,
                    Specs: specs,
                    Categories: genres,
                    Tags: tags,
                    AvgRating: rating
                ));
            }
        }
        catch (Exception e)
        {
            ownedGames.Add(new ErrorMessage(e, $"Exception while deserializing file {configFile.GetFullPath()}"));
        }
        return ownedGames;
    }
}
