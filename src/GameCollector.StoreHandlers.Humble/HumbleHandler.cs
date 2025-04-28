using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.Humble;

/// <summary>
/// Handler for finding games installed with Humble App.
/// </summary>
/// <remarks>
/// Uses json file:
///   %AppData%\Humble App\config.json
/// and Registry key:
///   HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall
/// </remarks>
/// <remarks>
/// Constructor.
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
public class HumbleHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<HumbleGame, HumbleGameId>
{
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    internal readonly JsonSerializerOptions JsonSerializerOptions = new()
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
    public override IEqualityComparer<HumbleGameId>? IdEqualityComparer => HumbleGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<HumbleGame, HumbleGameId> IdSelector => game => game.HumbleGameId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine64 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            using var regKey = localMachine64.OpenSubKey(Path.Combine(UninstallRegKey, "2f793df2-2969-529d-b0c0-7960ed40d70e"));
            if (regKey is not null)
            {
                if (regKey.TryGetString("DisplayIcon", out var icon))
                {
                    if (icon.Contains(',', StringComparison.Ordinal))
                        icon = icon[..icon.LastIndexOf(',')];
                    if (Path.IsPathRooted(icon))
                        return _fileSystem.FromUnsanitizedFullPath(icon);
                }
            }
        }

        return default;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override IEnumerable<OneOf<HumbleGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var configFile = _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .Combine("Humble App")
            .Combine("config.json");
        if (!configFile.FileExists)
        {
            yield return new ErrorMessage($"The configuration file {configFile.GetFullPath()} does not exist!");
            yield break;
        }

        using var stream = configFile.Read();
        var config = JsonSerializer.Deserialize<ConfigFile>(stream, JsonSerializerOptions);
        if (config is null)
        {
            yield return new ErrorMessage($"The configuration file {configFile.GetFullPath()} could not be deserialized!");
            yield break;
        }
        var hasChoice = false;
        var isPaused = false;
        if (config.User is not null)
        {
            if (config.User.HasPerks is not null &&
                (bool)config.User.HasPerks)
                hasChoice = true;
            if (config.User.IsPaused is not null &&
                (bool)config.User.IsPaused)
                isPaused = true;
        }
        if (config.GameCollection4 is not null)
        {
            foreach (var game in config.GameCollection4)
            {
                AbsolutePath launch = new();
                var isInstalled = false;
                var isExpired = false;
                var canInstall = true;
                var machineName = "";

                var id = game.DownloadMachineName;
                if (string.IsNullOrEmpty(id))
                    id = game.Gamekey ?? "";
                var name = game.GameName ?? "";

                if (id.EndsWith("_source", StringComparison.OrdinalIgnoreCase)) // skip source code
                {
                    if (settings?.GamesOnly == true)
                    {
                        yield return new ErrorMessage($"\"{game.GameName}\" is source code (not a game)!");
                        continue;
                    }
                    canInstall = false;
                }
                if (game.IsAvailable is not null &&
                    !(bool)game.IsAvailable) // must be downloaded from the website and installed manually
                {
                    if (settings?.GamesOnly == true)
                    {
                        yield return new ErrorMessage($"\"{game.GameName}\" must be downloaded from the website and installed manually.");
                        continue;
                    }
                    canInstall = false;
                }
                if (game.Status is not null &&
                    (game.Status.Equals("downloaded", StringComparison.OrdinalIgnoreCase) ||
                    game.Status.Equals("installed", StringComparison.OrdinalIgnoreCase)))
                {
                    if (settings?.InstalledOnly == true)
                        continue;
                    isInstalled = true;
                }
                if (game.MachineName is not null)
                {
                    machineName = game.MachineName;
                    if (machineName.EndsWith("_collection", StringComparison.OrdinalIgnoreCase) && // Has DRM; can only run/install during Choice subscription
                        (!hasChoice || isPaused))
                    {
                        if (settings?.OwnedOnly == true)
                            continue;
                        isExpired = true;
                    }
                    else if (machineName.EndsWith("_trove", StringComparison.OrdinalIgnoreCase) && // DRM-free; install only during Choice sub, but runs always
                        !isInstalled && (!hasChoice || isPaused))
                    {
                        if (settings?.OwnedOnly == true)
                            continue;
                        isExpired = true;
                    }
                }

                AbsolutePath path = new();
                if (Path.IsPathRooted(game.FilePath))
                {
                    path = _fileSystem.FromUnsanitizedFullPath(game.FilePath);
                    if (!string.IsNullOrEmpty(game.ExecutablePath))
                        launch = path.Combine(game.ExecutablePath);
                }

                var lastRunDate = DateTime.MinValue;
                if (game.LastPlayed is not null)
                {
                    if (long.TryParse(game.LastPlayed, CultureInfo.InvariantCulture, out var unixTime) &&
                        unixTime > 0)
                        lastRunDate = DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
                }

                var youtube = "";
                if (!string.IsNullOrEmpty(game.YoutubeLink))
                    youtube = game.YoutubeLink;
                else if (game.CarouselContent is not null &&
                    game.CarouselContent.YoutubeLink is not null &&
                    game.CarouselContent.YoutubeLink.Count > 0)
                {
                    youtube = game.CarouselContent.YoutubeLink[0];
                }

                List<string> publishers = [];
                if (game.Publishers is not null)
                {
                    foreach (var publisher in game.Publishers)
                    {
                        if (publisher.PublisherName is not null)
                            publishers.Add(publisher.PublisherName);
                    }
                }

                List<string> developers = [];
                if (game.Developers is not null)
                {
                    foreach (var developer in game.Developers)
                    {
                        if (developer.DeveloperName is not null)
                            publishers.Add(developer.DeveloperName);
                    }
                }

                yield return new HumbleGame(
                    HumbleGameId: HumbleGameId.From(id),
                    GameName: name,
                    FilePath: path,
                    ExecutablePath: launch,
                    LaunchUrl: $"humble://launch/{id}",
                    UninstallUrl: $"humble://uninstall/{id}",
                    LastPlayed: lastRunDate,
                    IsInstalled: isInstalled,
                    CanInstall: canInstall,
                    IsExpired: isExpired,
                    DescriptionText: game.DescriptionText ?? "",
                    IconPath: game.IconPath ?? "",
                    ImagePath: game.ImagePath ?? "",
                    Screenshots: game.CarouselContent?.Screenshot,
                    YouTubeLink: string.IsNullOrEmpty(youtube) ? null : $"https://www.youtube.com/watch?v={youtube}",
                    MachineName: machineName,
                    Developers: developers,
                    Publishers: publishers
                );
            }
        }
    }

    private static (string uninstall, string icon) GetRegValues(string id, IRegistry registry)
    {
        if (registry is null)
            return new();

        try
        {
            var currentUser = registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var unKey = currentUser.OpenSubKey(UninstallRegKey);
            if (unKey is null)
                return new();

            var subKeyNames = unKey.GetSubKeyNames().Where(
                keyName => keyName[(keyName.LastIndexOf('\\') + 1)..].StartsWith("Humble App ", StringComparison.OrdinalIgnoreCase)).ToList();
            if (subKeyNames.Count == 0)
                return new();

            foreach (var subKeyName in subKeyNames)
            {
                if (id.Equals(subKeyName[(subKeyName.IndexOf("Humble App ", StringComparison.OrdinalIgnoreCase) + 11)..], StringComparison.OrdinalIgnoreCase))
                {
                    using var subKey = unKey.OpenSubKey(subKeyName);
                    if (subKey is null)
                        return new();

                    _ = subKey.TryGetString("DisplayIcon", out var icon);
                    //_ = subKey.TryGetString("DisplayName", out var name);
                    //_ = subKey.TryGetString("InstallLocation", out var path);
                    _ = subKey.TryGetString("UninstallString", out var uninst);
                    return (uninst ?? "", icon ?? "");
                }
            }
        }
        catch (Exception) { }

        return new();
    }
}
