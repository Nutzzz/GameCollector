using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.BigFish;

/// <summary>
/// Handler for finding games installed with Big Fish Game Manager.
/// </summary>
/// <remarks>
/// Constructor. Uses Registry key:
///   HKLM32\SOFTWARE\Big Fish Games\Persistence
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
public class BigFishHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<BigFishGame, BigFishGameId>
{
    internal const string BigFishUrl = "https://www.bigfishgames.com/games/";
    internal const string BigFishRegKey = @"SOFTWARE\Big Fish Games";
    internal const string UninstallRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override IEqualityComparer<BigFishGameId>? IdEqualityComparer => BigFishGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<BigFishGame, BigFishGameId> IdSelector => game => game.ProductId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(Path.Combine(BigFishRegKey, "Client"));
            if (regKey is null) return default;

            if (regKey.TryGetString("InstallationPath", out var installPath) && Path.IsPathFullyQualified(installPath))
                return _fileSystem.FromUnsanitizedFullPath(installPath).Combine("bfgclient.exe");
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<BigFishGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

        using var bfgDbKey = localMachine32.OpenSubKey(Path.Combine(BigFishRegKey, "Persistence", "GameDB"));
        using var bfgInstKey = localMachine32.OpenSubKey(Path.Combine(BigFishRegKey, "Persistence", "Install"));
        if (bfgDbKey is null && bfgInstKey is null)
        {
            yield return new ErrorMessage($"Unable to open HKEY_LOCAL_MACHINE\\{BigFishRegKey}");
            yield break;
        }

        HashSet<string> subKeyNames = new(StringComparer.Ordinal);
        if (bfgInstKey is not null)
            subKeyNames.UnionWith(bfgInstKey.GetSubKeyNames());
        if (bfgDbKey is not null)
            subKeyNames.UnionWith(bfgDbKey.GetSubKeyNames());

        foreach (var subKeyName in subKeyNames)
        {
            var found = false;
            var name = "";
            AbsolutePath path = new();
            AbsolutePath launch = new();
            AbsolutePath icon = new();
            AbsolutePath uninstall = new();
            var lastAction = DateTime.MinValue;
            uint plays = 0;
            var activated = 0;
            var daysLeft = 0;
            var timeLeft = 0;
            var isInstalled = false;
            var isExpired = false;

            if (subKeyName.Equals("F7315T1L1", StringComparison.Ordinal))  // Skip "Big Fish Casino Activator")
                continue;

            if (bfgInstKey is not null)
            {
                using var subInst = bfgInstKey.OpenSubKey(subKeyName);
                if (subInst is not null)
                {
                    if (subInst.TryGetString("", out var exe) && Path.IsPathFullyQualified(exe))
                    {
                        launch = _fileSystem.FromUnsanitizedFullPath(exe);
                        if (launch.FileExists)
                        {
                            found = true;
                            isInstalled = true;
                        }
                        path = _fileSystem.FromUnsanitizedFullPath(launch.Directory);
                        name = path.GetFileNameWithoutExtension();
                    }
                }
            }

            if (bfgDbKey is not null)
            {
                using var subDb = bfgDbKey.OpenSubKey(subKeyName);
                if (subDb is not null && !subKeyName.Equals("F7315T1L1", StringComparison.Ordinal))  // Skip "Big Fish Casino Activator"
                {
                    _ = subDb.TryGetString("Name", out name);

                    if (subDb.TryGetValue("Activated", out var tmp))
                        _ = int.TryParse(tmp.ToString(), CultureInfo.InvariantCulture, out activated);
                    if (subDb.TryGetValue("DaysLeft", out tmp))
                        _ = int.TryParse(tmp.ToString(), CultureInfo.InvariantCulture, out daysLeft);
                    if (subDb.TryGetValue("TimeLeft", out tmp))
                        _ = int.TryParse(tmp.ToString(), CultureInfo.InvariantCulture, out timeLeft);

                    if (activated != 1 && (timeLeft > 0 || daysLeft > 0))  // expired trial
                    {
                        if (settings?.OwnedOnly != false)
                            continue;
                        isExpired = true;
                    }

                    if (!found && subDb.TryGetString("ExecutablePath", out var exe) && Path.IsPathFullyQualified(exe))
                    {
                        launch = _fileSystem.FromUnsanitizedFullPath(exe);
                        path = _fileSystem.FromUnsanitizedFullPath(launch.Directory);
                    }
                    if (subDb.TryGetString("feature", out var iconPath) && Path.IsPathFullyQualified(iconPath))     // 175x150
                        icon = _fileSystem.FromUnsanitizedFullPath(iconPath);
                    else if (subDb.TryGetString("Thumbnail", out iconPath) && Path.IsPathFullyQualified(iconPath))  // 80x80
                        icon = _fileSystem.FromUnsanitizedFullPath(iconPath);
                    else if (subDb.TryGetString("Icon", out iconPath) && Path.IsPathFullyQualified(iconPath))       // 60x40
                        icon = _fileSystem.FromUnsanitizedFullPath(iconPath);

                    if (subDb.TryGetValue("LastActionTime", out tmp))
                        lastAction = RegToDateTime((byte[])tmp);
                    if (subDb.TryGetValue("PlayCount", out tmp))
                        _ = uint.TryParse(tmp.ToString(), CultureInfo.InvariantCulture, out plays);
                }
            }

            using var unKey = _registry.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(UninstallRegKey);
            if (unKey is null)
                continue;

            foreach (var unKeyName in unKey.GetSubKeyNames().Where(
                keyName => keyName.StartsWith("BFG-", StringComparison.OrdinalIgnoreCase)))
            {
                using var subUnKey = unKey.OpenSubKey(unKeyName);
                if (subUnKey is null ||
                    !subUnKey.TryGetString("WrapID", out var wrap) ||
                    !wrap.Equals(subKeyName, StringComparison.Ordinal))

                    continue;

                if (icon == default)
                {
                    if (subUnKey.TryGetString("DisplayIcon", out var iconPath) && Path.IsPathFullyQualified(iconPath))
                        icon = _fileSystem.FromUnsanitizedFullPath(iconPath);
                }
                if (path == default)
                {
                    if (subUnKey.TryGetString("InstallLocation", out var pathStr) && Path.IsPathFullyQualified(pathStr))
                        path = _fileSystem.FromUnsanitizedFullPath(pathStr);
                }
                if (subUnKey.TryGetString("UninstallString", out var uninstPath) && Path.IsPathFullyQualified(uninstPath))
                    uninstall = _fileSystem.FromUnsanitizedFullPath(uninstPath.Trim('\"'));
            }

            if (icon == default && path != default && path.DirectoryExists())
                icon = _fileSystem.FromUnsanitizedFullPath(GetIconFromXml(path));

            yield return new BigFishGame(
                ProductId: BigFishGameId.From(subKeyName),
                Name: name ?? subKeyName,
                Path: path,
                ExecutablePath: launch,
                Icon: icon,
                Uninstall: uninstall,
                LastActionTime: lastAction,
                PlayCount: plays,
                IsInstalled: isInstalled,
                IsExpired: isExpired,
                ImageUrl: BigFishGame.GetImageUrl(subKeyName));
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code")]
    private static string GetIconFromXml(AbsolutePath path)
    {
        XmlSerializer serializer = new(typeof(GameState));
        using var stream = path.Read();
        var state = (GameState?)serializer.Deserialize(stream);
        if (state is not null)
        {
            if (state.Feature is not null)    // 175x150
                return state.Feature;
            if (state.Thumbnail is not null)  // 80x80
                return state.Thumbnail;
            if (state.Icon is not null)       // 60x40
                return state.Icon;
        }
        return "";
    }

    private static DateTime RegToDateTime(byte[] bytes)
    {
        // Note this only accounts for the first 4 bytes of a 16 byte span; not sure what the rest specifies
        var date = ((((
        (long)bytes[0]) * 256 +
        bytes[1]) * 256 +
        bytes[2]) * 256 +
        bytes[3]);
        return DateTimeOffset.FromUnixTimeSeconds(date - 2209032000).UtcDateTime; // This date is seconds from 1900 rather than 1970 epoch
    }
}
