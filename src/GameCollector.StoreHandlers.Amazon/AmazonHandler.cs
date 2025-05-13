using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.SQLiteUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using System.Diagnostics.CodeAnalysis;

namespace GameCollector.StoreHandlers.Amazon;

/// <summary>
/// Handler for finding games installed with Amazon Games.
/// </summary>
/// <remarks>
/// Constructor. Uses SQLite databases:
///   %LocalAppData%\Amazon Games\Data\Games\Sql\GameProductInfo.sqlite
///   %LocalAppData%\Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite
/// and Registry key:
///   HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall
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
public class AmazonHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<AmazonGame, AmazonGameId>
{
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override IEqualityComparer<AmazonGameId>? IdEqualityComparer => AmazonGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<AmazonGame, AmazonGameId> IdSelector => game => game.ProductId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(Path.Combine(UninstallRegKey, "{4DD10B06-78A4-4E6F-AA39-25E9C38FA568}"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("InstallLocation", out var app) && Path.IsPathFullyQualified(app))
                    return _fileSystem.FromUnsanitizedFullPath(app).Combine("Amazon Games.exe");
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<AmazonGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var prodDb = GetDatabasePath(_fileSystem).Combine("GameProductInfo.sqlite");
        var instDb = GetDatabasePath(_fileSystem).Combine("GameInstallInfo.sqlite");
        if (!prodDb.FileExists)
        {
            yield return new ErrorMessage($"The database file {prodDb} does not exist!");
            yield break;
        }
        if (!instDb.FileExists)
        {
            yield return new ErrorMessage($"The database file {instDb} does not exist!");
            yield break;
        }

        foreach (var game in ParseDatabase(prodDb, instDb, settings?.InstalledOnly ?? false))
        {
            if (game.IsGame())
            {
                yield return game;
            }
        }
    }

    private IEnumerable<OneOf<AmazonGame, ErrorMessage>> ParseDatabase(AbsolutePath prodDb, AbsolutePath instDb, bool installedOnly = false)
    {
        var owned = ParseOwned(prodDb, installedOnly);
        var installed = ParseInstalled(instDb, installedOnly);

        foreach (var install in installed.Values)
        {
            if (install.IsT0)
            {
                var game = install.AsT0;
                var id = game.ProductId;
                AmazonGame? ownedGame = null;
                if (owned.TryGetValue(id, out var value) && value.IsT0)
                    ownedGame = value.AsT0;

                yield return new AmazonGame(
                    ProductId: id,
                    ProductTitle: game.ProductTitle,
                    InstallDirectory: game.InstallDirectory,
                    LaunchUrl: game.LaunchUrl,
                    Icon: game.Icon,
                    Uninstall: game.Uninstall,
                    IsInstalled: game.IsInstalled,
                    InstallDate: game.InstallDate,
                    ReleaseDate: ownedGame?.ReleaseDate,
                    ProductDescription: ownedGame?.ProductDescription,
                    ProductIconUrl: ownedGame?.ProductIconUrl,
                    ProductLogoUrl: ownedGame?.ProductLogoUrl,
                    Screenshots: ownedGame?.Screenshots,
                    Videos: ownedGame?.Videos,
                    Developers: ownedGame?.Developers,
                    ProductPublisher: ownedGame?.ProductPublisher,
                    EsrbRating: ownedGame?.EsrbRating ?? EsrbRating.NO_RATING,
                    WebInfo: ownedGame?.WebInfo,
                    WebSupport: ownedGame?.WebSupport,
                    GameModes: ownedGame?.GameModes,
                    Genres: ownedGame?.Genres
                );
            }
            else
                yield return install.AsT1;
        }

        foreach (var own in owned.Values)
        {
            if (own.IsT0)
            {
                var game = own.AsT0;
                var id = game.ProductId;
                if (!installed.ContainsKey(id))
                {
                    yield return new AmazonGame(
                        ProductId: id,
                        ProductTitle: game.ProductTitle,
                        InstallDirectory: default,
                        LaunchUrl: "amazon-games://play/" + id,
                        IsInstalled: false,
                        ReleaseDate: game.ReleaseDate,
                        ProductDescription: game.ProductDescription,
                        ProductIconUrl: game.ProductIconUrl,
                        ProductLogoUrl: game.ProductLogoUrl,
                        Screenshots: game.Screenshots,
                        Videos: game.Videos,
                        Developers: game.Developers,
                        ProductPublisher: game.ProductPublisher,
                        EsrbRating: game.EsrbRating,
                        WebInfo: game.WebInfo,
                        WebSupport: game.WebSupport,
                        GameModes: game.GameModes,
                        Genres: game.Genres
                    );
                }
            }
            else
                yield return own.AsT1;
        }
    }

    private Dictionary<AmazonGameId, OneOf<AmazonGame, ErrorMessage>> ParseInstalled(AbsolutePath db, bool installedOnly = false)
    {
        Dictionary<AmazonGameId, OneOf<AmazonGame, ErrorMessage>> installDict = [];

        var installs = SQLiteHelpers.GetDataTable(db, "SELECT * FROM DbSet;").ToList<InstallInfo>();
        if (installs is not null)
        {
            AbsolutePath path = new();
            AbsolutePath icon = new();
            AbsolutePath uninstall = new();
            var isInstalled = true;

            var i = 0;
            foreach (var install in installs)
            {
                i++;
                if (install.Installed.Equals(0)) // was installed, now uninstalled
                {
                    if (installedOnly)
                        continue;
                    isInstalled = false;
                }

                var strId = install.Id;
                if (strId is null)
                {
                    installDict.TryAdd(AmazonGameId.From(i.ToString(CultureInfo.InvariantCulture)),
                        new ErrorMessage($"Value for \"ProductIdStr\" does not exist in file {db}"));
                    continue;
                }

                var id = AmazonGameId.From(strId);
                var dir = install.InstallDirectory;
                if (dir is not null && Path.IsPathFullyQualified(dir))
                    path = _fileSystem.FromUnsanitizedFullPath(dir);
                else
                {
                    if (installedOnly)
                    {
                        installDict.TryAdd(AmazonGameId.From(i.ToString(CultureInfo.InvariantCulture)),
                            new ErrorMessage($"Value for \"InstallDirectory\" does not exist in file {db}"));
                        continue;
                    }
                    isInstalled = false;
                }

                _ = DateTime.TryParseExact(install.InstallDate, "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dtInstallDate);

                if (isInstalled)
                {
                    icon = ParseFuelFileForExe(path);
                    var regGame = ParseRegistryForId(_fileSystem, _registry, strId);
                    if (regGame.IsGame())
                    {
                        if (icon == default)
                            icon = regGame.AsGame().Icon;
                        uninstall = regGame.AsGame().Uninstall;
                    }
                }

                installDict.TryAdd(id, new AmazonGame(
                    ProductId: id,
                    ProductTitle: install.ProductTitle,
                    InstallDirectory: path,
                    Icon: icon,
                    Uninstall: uninstall,
                    IsInstalled: isInstalled,
                    InstallDate: dtInstallDate
                ));
            }
        }

        return installDict;
    }

    private static Dictionary<AmazonGameId, OneOf<AmazonGame, ErrorMessage>> ParseOwned(AbsolutePath db, bool installedOnly = false)
    {
        Dictionary<AmazonGameId, OneOf<AmazonGame, ErrorMessage>> ownedDict = [];

        var products = SQLiteHelpers.GetDataTable(db, "SELECT * FROM DbSet;").ToList<ProductInfo>();
        if (products is not null)
        {
            var i = 0;
            foreach (var product in products)
            {
                i++;
                var strId = product.ProductIdStr;
                if (strId is null)
                {
                    ownedDict.TryAdd(AmazonGameId.From(i.ToString(CultureInfo.InvariantCulture)),
                        new ErrorMessage($"Value for \"ProductIdStr\" does not exist in file {db}"));
                    continue;
                }

                var id = AmazonGameId.From(strId);
                _ = DateTime.TryParseExact(product.ReleaseDate, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dtReleaseDate);
                _ = Enum.TryParse(product.EsrbRating ?? "NO_RATING", out EsrbRating ageRating);

                if (ownedDict.ContainsKey(id))
                {
                    ownedDict.TryAdd(AmazonGameId.From(i.ToString(CultureInfo.InvariantCulture)),
                        new ErrorMessage($"Item with value \"ProductIdStr\" already exists"));
                    continue;
                }

                ownedDict.TryAdd(id, new AmazonGame(
                    ProductId: id,
                    ProductTitle: product.ProductTitle,
                    InstallDirectory: default,
                    ReleaseDate: dtReleaseDate,
                    ProductDescription: product.ProductDescription,
                    ProductIconUrl: product.ProductIconUrl,
                    ProductLogoUrl: product.ProductLogoUrl,
                    Screenshots: product.ScreenshotsJson ?? "",
                    Videos: product.VideosJson ?? "",
                    Developers: product.DevelopersJson ?? "",
                    ProductPublisher: product.ProductPublisher,
                    EsrbRating: ageRating,
                    WebInfo: GetWebInfo(product.ExternalWebsitesJson ?? ""),
                    WebSupport: GetWebSupport(product.ExternalWebsitesJson ?? ""),
                    GameModes: product.GameModesJson ?? "",
                    Genres: product.GenresJson ?? ""
                ));
            }
        }

        return ownedDict;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private static string? GetWebInfo(string json)
    {
        using var doc = JsonDocument.Parse(json, new() { AllowTrailingCommas = true, });
        var websites = doc.Deserialize<ExternalWebsites>();

        if (websites is null)
            return null;

        if (!IsInvalid(websites.Official))
            return websites.Official;
        if (!IsInvalid(websites.Wikia))
            return websites.Wikia;
        if (!IsInvalid(websites.Igdb))
            return websites.Igdb;
        if (!IsInvalid(websites.Wikipedia))
            return websites.Wikipedia;
        if (!IsInvalid(websites.Steam))
            return websites.Steam;
        if (!IsInvalid(websites.Epicgames))
            return websites.Epicgames;
        if (!IsInvalid(websites.Gog))
            return websites.Gog;
        if (!IsInvalid(websites.Itch))
            return websites.Itch;
        if (!IsInvalid(websites.Android))
            return websites.Android;
        if (!IsInvalid(websites.Ipad))
            return websites.Ipad;
        if (!IsInvalid(websites.Iphone))
            return websites.Iphone;
        if (!IsInvalid(websites.Instagram))
            return websites.Instagram;
        if (!IsInvalid(websites.Youtube))
            return websites.Youtube;
        if (!IsInvalid(websites.Twitter))
            return websites.Twitter;
        if (!IsInvalid(websites.Twitch))
            return websites.Twitch;
        if (!IsInvalid(websites.Reddit))
            return websites.Reddit;
        if (!IsInvalid(websites.Facebook))
            return websites.Facebook;
        return "[\"https://www.twitch.tv/\"]";
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private static string? GetWebSupport(string json)
    {
        using var doc = JsonDocument.Parse(json, new() { AllowTrailingCommas = true, });
        var websites = doc.Deserialize<ExternalWebsites>();

        if (websites is null)
            return null;

        if (!IsInvalid(websites.Support))
            return websites.Support;
        if (!IsInvalid(websites.Twitch))
            return websites.Twitch;
        if (!IsInvalid(websites.Reddit))
            return websites.Reddit;
        if (!IsInvalid(websites.Facebook))
            return websites.Facebook;
        if (!IsInvalid(websites.Official))
            return websites.Official;
        return "[\"https://www.twitch.tv/\"]";
    }

    private static bool IsInvalid(string? websites)
    {
        return string.IsNullOrEmpty(websites) ||
            websites.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    private OneOf<AmazonGame, ErrorMessage>[] ParseRegistry()
    {
        if (_registry is null)
        {
            return [new ErrorMessage("Unable to open registry"),];
        }

        try
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var unKey = currentUser.OpenSubKey(UninstallRegKey);
            if (unKey is null)
            {
                return [new ErrorMessage($"Unable to open HKEY_CURRENT_USER\\{UninstallRegKey}"),];
            }

            var subKeyNames = unKey.GetSubKeyNames().Where(
                keyName => keyName[(keyName.LastIndexOf('\\') + 1)..].StartsWith("AmazonGames/", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (subKeyNames.Length == 0)
            {
                return [new ErrorMessage($"Registry key {unKey.GetName()} has no sub-keys beginning with \"AmazonGames/\""),];
            }

            return subKeyNames
                .Select(subKeyName => ParseSubKey(unKey, subKeyName, _fileSystem))
                .ToArray();
        }
        catch (Exception e)
        {
            return [new ErrorMessage(e, "Exception looking for Amazon games in registry")];
        }
    }

    private static OneOf<AmazonGame, ErrorMessage> ParseRegistryForId(IFileSystem fileSystem, IRegistry registry, string id)
    {
        if (registry is null)
        {
            return new ErrorMessage("Unable to open registry");
        }

        try
        {
            var currentUser = registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var unKey = currentUser.OpenSubKey(UninstallRegKey);
            if (unKey is null)
            {
                return new ErrorMessage($"Unable to open HKEY_CURRENT_USER\\{UninstallRegKey}");
            }

            var subKeyNames = unKey.GetSubKeyNames().Where(
                keyName => keyName[(keyName.LastIndexOf('\\') + 1)..].StartsWith("AmazonGames/", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (subKeyNames.Length == 0)
            {
                return new ErrorMessage($"Registry key {unKey.GetName()} has no sub-keys beginning with \"AmazonGames/\"");
            }

            foreach (var subKeyName in subKeyNames)
            {
                var game = ParseSubKey(unKey, subKeyName, fileSystem, id);
                if (game.IsGame())
                {
                    return game;
                }
            }
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, "Exception looking for Amazon games in registry");
        }
        return new ErrorMessage("ID not found");
    }

    private static AbsolutePath ParseFuelFileForExe(AbsolutePath dir)
    {
        try
        {
            var file = dir.Combine("fuel.json");
            if (file.FileExists)
            {
                var strDocumentData = File.ReadAllText(file.GetFullPath());

                if (!string.IsNullOrEmpty(strDocumentData))
                {
                    using var document = JsonDocument.Parse(@strDocumentData, new() { AllowTrailingCommas = true, });
                    var root = document.RootElement;
                    if (root.TryGetProperty("Main", out var main) && main.TryGetProperty("Command", out var command))
                    {
                        var exe = command.GetString();
                        if (!string.IsNullOrEmpty(exe))
                            return dir.Combine(exe);
                    }
                }
            }
        }
        catch (Exception) { }

        return default;
    }

    private static OneOf<AmazonGame, ErrorMessage> ParseSubKey(IRegistryKey unKey, string subKeyName, IFileSystem fileSystem, string id = "")
    {
        try
        {
            using var subKey = unKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                return new ErrorMessage($"Unable to open {unKey}\\{subKeyName}");
            }

            if (!subKey.TryGetString("UninstallString", out var uninst))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"UninstallString\"");
            }
            var gameId = uninst[uninst.LastIndexOf("Game -p " + 8, StringComparison.OrdinalIgnoreCase)..];
            if (!string.IsNullOrEmpty(id) && !id.Equals(gameId, StringComparison.OrdinalIgnoreCase))
            {
                return new ErrorMessage("ID does not match.");
            }

            if (!subKey.TryGetString("DisplayName", out var name))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"DisplayName\"");
            }

            if (!subKey.TryGetString("InstallLocation", out var path))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"InstallLocation\"");
            }

            if (!subKey.TryGetString("DisplayIcon", out var launch)) launch = "";

            if (!subKey.TryGetString("Publisher", out var pub)) pub = "";

            return new AmazonGame(
                ProductId: AmazonGameId.From(gameId),
                ProductTitle: name,
                InstallDirectory: Path.IsPathFullyQualified(path) ? fileSystem.FromUnsanitizedFullPath(path) : new(),
                Command: Path.IsPathFullyQualified(launch) ? fileSystem.FromUnsanitizedFullPath(launch) : new(),
                Uninstall: Path.IsPathFullyQualified(uninst) ? fileSystem.FromUnsanitizedFullPath(uninst) : new(),
                ProductPublisher: pub
            );
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {unKey}\\{subKeyName}");
        }
    }

    internal static AbsolutePath GetDatabasePath(IFileSystem fileSystem)
    {
        return fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .Combine("Amazon Games")
            .Combine("Data")
            .Combine("Games")
            .Combine("Sql");
    }
}
