using System;
using System.Buffers.Text;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using GameFinder.Common;
using NexusMods.Paths;
using OneOf;
using static System.Environment;

namespace GameCollector.StoreHandlers.EGS;

public partial class EGSHandler : AHandler<EGSGame, EGSGameId>
{
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private static List<OneOf<EGSGame, ErrorMessage>> ParseCatCacheFile(IFileSystem fileSystem, Settings? settings = null)
    {
        List<OneOf<EGSGame, ErrorMessage>> games = [];
        var catalogPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Catalog\catcache.bin");
        if (!File.Exists(catalogPath))
        {
            games.Add(new ErrorMessage($"File does not exist: {catalogPath}"));
            return games;
        }

        try
        {
            Span<byte> byteSpan = File.ReadAllBytes(catalogPath);
            var os = Base64.DecodeFromUtf8InPlace(byteSpan, out var numBytes);
            if (os == OperationStatus.Done)
            {
                byteSpan = byteSpan[..numBytes];
                var plaintext = Encoding.UTF8.GetString(byteSpan);
#if DEBUG
                File.WriteAllText($"egs_catalog.json", plaintext);
#endif
                using var document = JsonDocument.Parse(plaintext, new() { AllowTrailingCommas = true });
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var game = JsonSerializer.Deserialize<Catalog>(element, JsonSerializerOptions);
                    if (game is null)
                    {
                        games.Add(new ErrorMessage($"Unable to deserialize CatCacheFile {catalogPath}"));
                        continue;
                    }

                    var id = "";
                    var title = "";
                    var space = "";
                    var imageUrl = "";
                    var wideImageUrl = "";
                    var appId = "";
                    var savePath = "";
                    string? mainGame = null;

                    id = game.Id ?? "";
                    if (string.IsNullOrEmpty(id))
                        continue;

                    // File seems to be encoded to Base64 improperly, so we need to remove Unicode replacement character
                    title = (game.Title ?? "").Replace("?", "", StringComparison.Ordinal) ?? id;
                    if (string.IsNullOrEmpty(title))
                        title = id;

                    if (game.MainGameItem is not null && !string.IsNullOrEmpty(game.MainGameItem.Id))
                    {
                        if (settings?.BaseOnly == true)
                        {
                            games.Add(new ErrorMessage($"\"{title}\" is a DLC of {game.MainGameItem.Id}"));
                            continue;
                        }
                        mainGame = game.MainGameItem.Id;
                    }

                    space = game.Namespace ?? "";
                    // skip Twinmotion
                    if (space.Equals("poodle", StringComparison.OrdinalIgnoreCase))
                        continue;

                    List<string> genres = [];
                    if (game.Categories is not null)
                    {
                        var isGame = false;
                        var audience = false;
                        var engines = false;

                        foreach (var category in game.Categories)
                        {
                            // skip "audience" and "engines" categories (but "games" are OK, and possibly "software", "applications", etc.)
                            if (category is not null && category.Path is not null)
                            {
                                if (category.Path.Equals("games", StringComparison.OrdinalIgnoreCase))
                                {
                                    isGame = true;
                                    break;
                                }
                                if (category.Path.Equals("audience", StringComparison.OrdinalIgnoreCase))
                                {
                                    audience = true;
                                    break;
                                }
                                if (category.Path.Equals("engines", StringComparison.OrdinalIgnoreCase))
                                {
                                    engines = true;
                                    break;
                                }
                                genres.Add(category?.Name ?? "");
                            }
                        }
                        if (audience)
                            continue;
                        if (engines)
                        {
                            games.Add(new ErrorMessage($"\"{title}\" is game engine-related"));
                            continue;
                        }
                        if (settings?.GamesOnly == true && !isGame)
                        {
                            games.Add(new ErrorMessage($"\"{title}\" is not a game (e.g., an application or software)"));
                            continue;
                        }
                    }

                    if (game.ReleaseInfo is not null)
                    {
                        foreach (var info in game.ReleaseInfo)
                        {
                            appId = info.AppId ?? "";
                        }
                    }

                    if (game.CustomAttributes is not null && game.CustomAttributes.CloudSaveFolder is not null)
                        savePath = game.CustomAttributes.CloudSaveFolder.Value ?? "";

                    if (game.KeyImages is not null)
                    {
                        foreach (var image in game.KeyImages)
                        {
                            if (image is not null && image.Type is not null)
                            {
                                imageUrl = image.Type.Equals("DieselGameBoxTall", StringComparison.OrdinalIgnoreCase) ? image.Url ?? "" : "";
                                wideImageUrl = image.Type.Equals("DieselGameBox", StringComparison.OrdinalIgnoreCase) ? image.Url ?? "" : "";
                            }
                        }
                    }

                    games.Add(new EGSGame(
                        CatalogItemId: EGSGameId.From(id),
                        DisplayName: title,
                        InstallLocation: new(),
                        CloudSaveFolder: Path.IsPathRooted(savePath) ? fileSystem.FromUnsanitizedFullPath(savePath) : new(),
                        IsInstalled: false,
                        MainGame: mainGame,
                        ImageTallUrl: imageUrl,
                        ImageUrl: wideImageUrl,
                        Developer: game.Developer ?? "",
                        Categories: genres,
                        Namespace: space,
                        AppId: appId));
                }
            }
        }
        catch (Exception e)
        {
            games.Add(new ErrorMessage(e, $"Exception while decoding file {catalogPath}"));
        }
        return games;
    }

    private static List<OneOf<EGSGame, ErrorMessage>> GetOwnedGames(
        Dictionary<EGSGameId, OneOf<EGSGame, ErrorMessage>> installedDict,
        IFileSystem fileSystem,
        Settings? settings = null)
    {
        List<OneOf<EGSGame, ErrorMessage>> ownedList = [];
        List<OneOf<EGSGame, ErrorMessage>> installedList = [];
        Dictionary<string, EGSGameId> namespaces = new(StringComparer.OrdinalIgnoreCase);

        var ownedGames = ParseCatCacheFile(fileSystem, settings);
        foreach (var game in ownedGames)
        {
            if (game.IsT0)
                namespaces.TryAdd(game.AsT0.Namespace, game.AsT0.CatalogItemId);
        }
        foreach (var game in ownedGames)
        {
            if (game.IsT1)
            {
                ownedList.Add(game);
                continue;
            }
            var id = game.AsT0.CatalogItemId;
            var mainGame = game.AsT0.MainGame;
            if (!installedDict.ContainsKey(id))
            {
                if (string.IsNullOrEmpty(mainGame) && namespaces.TryGetValue(game.AsT0.Namespace, out var value) && !id.Equals(value))
                {
                    ownedList.Add(new ErrorMessage($"\"{game.AsT0.DisplayName}\" is a DLC or alternate install of {value}"));
                    if (settings?.BaseOnly == true)
                        continue;
                    // TODO: Do I want this falling through?
                    mainGame = value.ToString();
                }
                if (settings?.InstalledOnly != true)
                {
                    ownedList.Add(game);
                    continue;
                }
            }
            installedList.Add(new EGSGame(
                CatalogItemId: id,
                DisplayName: installedDict[id].AsT0.DisplayName,
                InstallLocation: installedDict[id].AsT0.InstallLocation,
                CloudSaveFolder: game.AsT0.CloudSaveFolder,
                InstallLaunch: installedDict[id].AsT0.InstallLaunch,
                IsInstalled: true,
                MainGame: mainGame,
                ImageTallUrl: game.AsT0.ImageTallUrl,
                ImageUrl: game.AsT0.ImageUrl,
                Developer: game.AsT0.Developer,
                Namespace: game.AsT0.Namespace,
                AppId: game.AsT0.AppId));
        }

        foreach (var error in installedDict)
        {
            if (error.Value.IsT1)
                installedList.Add(error.Value);
        }

        /*
        foreach (var installed in installedList)
        {
            if (installed.IsT0)
            {
                foreach (var owned in ownedList)
                {
                    if (owned.IsT0 && owned.AsT0.Namespace.Equals(installed.AsT0.Namespace, StringComparison.OrdinalIgnoreCase))
                    {
                        _ = ownedList.Remove(owned);
                        if (!baseOnly)
                        {
                            ownedList.Add(new EGSGame(
                                CatalogItemId: owned.AsT0.CatalogItemId,
                                DisplayName: owned.AsT0.DisplayName,
                                InstallLocation: default,
                                IsInstalled: false,
                                MainGame: installed.AsT0.CatalogItemId.ToString(),
                                ImageTallUrl: owned.AsT0.ImageTallUrl,
                                ImageUrl: owned.AsT0.ImageUrl,
                                Developer: owned.AsT0.Developer,
                                Namespace: owned.AsT0.Namespace,
                                AppId: owned.AsT0.AppId));
                        }
                        else
                            ownedList.Add(new ErrorMessage($"{owned.AsT0.DisplayName} is a DLC or alternate install"));

                        break;
                    }
                }
            }
        }
        */

        ownedList.AddRange(installedList);
        return ownedList;
    }
}
