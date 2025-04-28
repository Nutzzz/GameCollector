using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GameCollector.StoreHandlers.Ubisoft;

/// <summary>
/// Handler for finding games installed with Ubisoft Connect.
/// </summary>
/// <remarks>
/// Uses registry:
///   HKLM32\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
/// And yaml file:
///   .\cache\configuration\configurations
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
public class UbisoftHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<UbisoftGame, UbisoftGameId>
{
    internal const string ConnectRegKey = @"SOFTWARE\Ubisoft\Launcher";
    internal const string UninstallRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <summary>
    /// The supported schema version of this handler. You can change the schema policy with
    /// <see cref="SchemaPolicy"/>.
    /// </summary>
    public const decimal SupportedSchemaVersion = 2.0M;

    /// <summary>
    /// Policy to use when the schema version does not match <see cref="SupportedSchemaVersion"/>.
    /// The default behavior is <see cref="Ubisoft.SchemaPolicy.Warn"/>.
    /// </summary>
    public SchemaPolicy SchemaPolicy { get; set; } = SchemaPolicy.Warn;

    /// <inheritdoc/>
    public override IEqualityComparer<UbisoftGameId>? IdEqualityComparer => UbisoftGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<UbisoftGame, UbisoftGameId> IdSelector => game => game.GameCode;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(ConnectRegKey);
            if (regKey is not null)
            {
                if (regKey.TryGetString("InstallDir", out var install) && Path.IsPathRooted(install))
                    return _fileSystem.FromUnsanitizedFullPath(install).Combine("UbisoftConnect.exe");
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<UbisoftGame, ErrorMessage>> FindAllGames(GameFinder.Common.Settings? settings = null)
    {
        var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

        using var unKey = localMachine32.OpenSubKey(UninstallRegKey);
        if (unKey is null)
        {
            yield return new ErrorMessage($"Unable to open HKEY_LOCAL_MACHINE\\{UninstallRegKey}");
            yield break;
        }

        var subKeyNames = unKey.GetSubKeyNames().Where(
            keyName => keyName[(keyName.LastIndexOf('\\') + 1)..].StartsWith("Uplay Install ", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (subKeyNames.Length == 0)
        {
            yield return new ErrorMessage($"Registry key {unKey.GetName()} has no sub-keys beginning with \"Uplay Install \"");
            yield break;
        }

        List<string> ubiIds = [];
        foreach (var subKeyName in subKeyNames)
        {
            yield return ParseSubKey(unKey, subKeyName, _fileSystem, out var iconId);
            if (iconId is not null)
                ubiIds.Add(iconId);

        }

        var ubiKey = unKey.OpenSubKey("Uplay");
        if (ubiKey is null || !ubiKey.TryGetString("InstallLocation", out var launcherPath))
        {
            yield break;
        }

        if (settings?.InstalledOnly == true) yield break;

        // Get owned but not-installed games

        var configFile = _fileSystem.FromUnsanitizedFullPath(Path.Combine(launcherPath, "cache", "configuration", "configurations"));
        if (configFile.FileExists)
        {
            // This file is mostly yaml text, but there is binary before each entry that I attempt to strip out
            // Each entry is expected to start with "version:" (schema)
            using var stream = _fileSystem.ReadFile(configFile);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            List<string> input = [];
            do
            {
                var parse = false;
                var line = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        line = reader.ReadLine();
                        continue;
                    }
                    if (line.Contains("version: ", StringComparison.Ordinal) &&
                        !line.Contains(" version:", StringComparison.Ordinal))
                    {
                        line = line[line.IndexOf("version: ", StringComparison.Ordinal)..];
                        /*
                        var sVersion = line[(line.IndexOf("version: ", StringComparison.Ordinal) + 9)..];
                        if (decimal.TryParse(sVersion, CultureInfo.InvariantCulture, out var version) &&
                            version < SupportedSchemaVersion)
                        {
                            var (schemaMessage, isSchemaError) = CreateSchemaVersionMessage(SchemaPolicy, version, configFile.FullName);
                            if (schemaMessage is not null)
                            {
                                yield return new ErrorMessage(schemaMessage);
                                if (isSchemaError)
                                    yield break;
                                break;
                            }
                        }
                        */
                        break;
                    }

                    if (line.Contains('�', StringComparison.Ordinal) ||
                        line.Contains('', StringComparison.Ordinal) ||
                        //line.Contains('~', StringComparison.Ordinal) ||
                        line.StartsWith('#') ||
                        line.EndsWith("#--------------------------------------------------------------------------", StringComparison.Ordinal))
                    {
                        line = reader.ReadLine();
                        continue;
                    }
                    parse = true;
                    input.Add(line);
                    line = reader.ReadLine();
                }
                if (parse)
                {
                    yield return ParseConfigFile(string.Join('\n', input), launcherPath, _fileSystem, _registry, ubiIds, settings?.BaseOnly);
                }
                input = [];
                if (line is not null &&
                    line.Contains("version: ", StringComparison.Ordinal) &&
                    !line.Contains(" version:", StringComparison.Ordinal))
                {
                    input.Add(line);
                }
            }
            while (!reader.EndOfStream);
        }
    }

    private static OneOf<UbisoftGame, ErrorMessage> ParseConfigFile(string input, string launcherPath, IFileSystem fileSystem, IRegistry registry, List<string> ubiIds, bool? baseOnly = false)
    {
        ConfigFile config;
        var id = "";
        string name;
        AbsolutePath path = new();
        AbsolutePath launch = new();
        AbsolutePath icon = new();
        string iconFile;
        var isInstalled = false;

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            config = deserializer.Deserialize<ConfigFile>(input);
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Malformed YAML in configurations file entry\n{e.InnerException}");
        }

        try
        {
            if (config is null || config.Root is null)
                return new ErrorMessage("No \"root\" property in configurations file entry");

            /*
            if (!config.Version.HasValue)
            {
                //return null;
                return new ErrorMessage("No schema \"version\" property in configurations file entry");
            }
            */

            name = config.Root.DisplayName ?? "";
            if (string.IsNullOrEmpty(name))
                name = config.Root.Name ?? "";
            if (string.IsNullOrEmpty(name))
                return new ErrorMessage($"No \"root>name\" property in configurations file entry");

            // fallback ID (if available, we'll get the icon filename instead)
            if (config.Root.Uplay is not null)
            {
                if (config.Root.Uplay.GameCode is not null)
                    id = config.Root.Uplay.GameCode;
                else if (config.Root.Uplay.AchievementsSyncId is not null)
                    id = config.Root.Uplay.AchievementsSyncId;
            }

            iconFile = config.Root.IconImage ?? "";
            if (string.IsNullOrEmpty(iconFile))
                iconFile = config.Root.ThumbImage ?? "";

            if (config.Localizations is not null && config.Localizations.Default is not null)
            {
                name = Localize(name, config.Localizations.Default);
                iconFile = Localize(iconFile, config.Localizations.Default);
            }

            if (string.IsNullOrEmpty(name) &&
                config.Root.Installer is not null &&
                config.Root.Installer.GameIdentifier is not null)
            {
                name = config.Root.Installer.GameIdentifier;
            }

            var isDLC = false;
            if (config.Root.StartGame is null ||
                (config.Root.IsDlc is not null && ToBool(config.Root.IsDlc)) ||
                (config.Root.IsUlc is not null && ToBool(config.Root.IsUlc)) ||
                (config.Root.OptionalAddonEnabledByDefault is not null &&
                ToBool(config.Root.OptionalAddonEnabledByDefault)))
            {
                if (baseOnly == true)
                    return new ErrorMessage($"\"{name}\" is not a base game!");
                isDLC = true;
            }

            /*
            if (config.Root.ThirdPartyPlatform is not null)
                return new ErrorMessage($"\"{name}\" [{id}] is a third-party platform game!"); // e.g., a Steam game
            */

            if (iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                iconFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                iconFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                    name = id;
                if (!string.IsNullOrEmpty(iconFile))
                    id = Path.GetFileNameWithoutExtension(iconFile);
            }
            if (string.IsNullOrEmpty(id))
            {
                if (baseOnly == true)
                    return new ErrorMessage($"\"{name}\" does not have an ID!");

                if (string.IsNullOrEmpty(name))
                    return new ErrorMessage("Entry does not have a name or ID!");

                id = new string(name.Where(char.IsLetterOrDigit).ToArray());
            }
            else
            {
                if (ubiIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    return new ErrorMessage($"\"{name}\" was already found!");

                if (string.IsNullOrEmpty(name))
                    name = id;
            }

            // See if path and exe can be found in registry
            if (config.Root.StartGame is not null)
            {
                var configGame = config.Root.StartGame.Online;
                if (configGame is not null &&
                    configGame.Executables is not null &&
                    configGame.Executables.Count > 0)
                {
                    if (configGame.Executables[0] is not null &&
                        configGame.Executables[0].WorkingDirectory is not null)
                    {
                        var configPath = configGame.Executables[0].WorkingDirectory;
                        if (configPath is not null && configPath.Register is not null)
                        {
                            var sRegistry = configPath.Register;
                            if (sRegistry.Contains('\\', StringComparison.Ordinal))
                            {
                                var hive32 = registry.OpenBaseKey(
                                    RegistryHelpers.RegistryHiveFromString(
                                        sRegistry[..sRegistry.IndexOf('\\', StringComparison.OrdinalIgnoreCase)]),
                                    RegistryView.Registry32);
                                var sRegKey = sRegistry[(sRegistry.IndexOf('\\', StringComparison.Ordinal) + 1)..sRegistry.LastIndexOf('\\')];
                                using var regKey = hive32.OpenSubKey(sRegKey);
                                if (regKey is not null)
                                {
                                    var value = regKey.GetValue(Path.GetFileName(sRegistry));
                                    if (value is not null)
                                    {
                                        path = fileSystem.FromUnsanitizedFullPath(value.ToString() ?? "");
                                        var configExe = configGame.Executables[0].Path;
                                        if (configExe is not null && configExe.Relative is not null)
                                            launch = path.Combine(configExe.Relative);
                                        if (fileSystem.FileExists(launch))
                                            isInstalled = true;
                                    }

                                }
                            }
                        }
                    }
                }
            }

            return new UbisoftGame(
                GameCode: UbisoftGameId.From(id),
                DisplayName: name,
                InstallPath: path,
                Executable: launch,
                Icon: icon,
                Uninstall: new(),
                IsInstalled: isInstalled,
                IsDLC: isDLC);
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing configurations file entry\n{e.InnerException}");
        }
    }

    internal static (string? message, bool isError) CreateSchemaVersionMessage(
        SchemaPolicy schemaPolicy, decimal schemaVersion, string configFilePath)
    {
        if (schemaVersion == SupportedSchemaVersion) return (null, false);

        return schemaPolicy switch
        {
            SchemaPolicy.Warn => (
                $"File {configFilePath} has a schema version " +
                $"{schemaVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports schema version " +
                $"{SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This message is a WARNING because the consumer of this library has set {nameof(SchemaPolicy)} to {nameof(SchemaPolicy.Warn)}",
                false),
            SchemaPolicy.Error => (
                $"File {configFilePath} has a schema version " +
                $"{schemaVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports schema version " +
                $"{SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This is an ERROR because the consumer of this library has set {nameof(SchemaPolicy)} to {nameof(SchemaPolicy.Error)}",
                true),
            SchemaPolicy.Ignore => (null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaPolicy), schemaPolicy, message: null),
        };
    }

    private static string Localize(string key, ConfigLocalizeLang? lang)
    {
        if (lang is not null)
        {
            switch (key)
            {
                case "NAME":
                    return lang.Name ?? "";
                case "GAMENAME":
                    return lang.Gamename ?? "";
                case "ICONIMAGE":
                    return lang.Iconimage ?? "";
                case "THUMBIMAGE":
                    return lang.Thumbimage ?? "";
                case "DESCRIPTION":
                    return lang.Description ?? "";
                case "l1":
                    return lang.Localize1 ?? "";
                case "l2":
                    return lang.Localize2 ?? "";
                case "l3":
                    return lang.Localize3 ?? "";
                case "l4":
                    return lang.Localize4 ?? "";
                case "l5":
                    return lang.Localize5 ?? "";
                default:
                    break;
            }
        }
        return key;
    }

    private static bool ToBool(string key, bool bDefault = false)
    {
        return key.ToLower(CultureInfo.InvariantCulture) switch
        {
            "y" or "yes" or "true" or "on" => true,
            "n" or "no" or "false" or "off" => false,
            _ => bDefault,
        };
    }

    private static OneOf<UbisoftGame, ErrorMessage> ParseSubKey(IRegistryKey unKey, string subKeyName, IFileSystem fileSystem, out string? sId)
    {
        sId = "";
        try
        {
            using var subKey = unKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                return new ErrorMessage($"Unable to open {unKey}\\{subKeyName}");
            }

            sId = subKeyName[14..];
            if (!int.TryParse(sId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return new ErrorMessage($"The subkey name of {subKey.GetName()} does not end with a number: \"{sId}\"");
            }

            if (!subKey.TryGetString("InstallLocation", out var path))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"InstallLocation\"");
            }

            if (!subKey.TryGetString("DisplayName", out var name))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"DisplayName\"");
            }

            var url = "uplay://launch/" + id.ToString(CultureInfo.InvariantCulture);
            if (!subKey.TryGetString("DisplayIcon", out var icon))
                icon = "";
            if (icon.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                sId = Path.GetFileNameWithoutExtension(icon);
            var uninstallArgs = "";
            if (!subKey.TryGetString("UninstallString", out var uninstall))
                uninstall = "";
            else
            {
                if (uninstall.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = uninstall.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                    uninstallArgs = uninstall[(idx + 4)..].TrimStart('\"').TrimStart(' ');
                    uninstall = uninstall[..(idx + 5)].TrimStart('\"');
                }
            }

            return new UbisoftGame(
                GameCode: UbisoftGameId.From(sId),
                DisplayName: name,
                InstallPath: fileSystem.FromUnsanitizedFullPath(path),
                LaunchUrl: url,
                Icon: fileSystem.FromUnsanitizedFullPath(icon),
                Uninstall: string.IsNullOrEmpty(uninstall) ? new() : fileSystem.FromUnsanitizedFullPath(uninstall),
                UninstallArgs: uninstallArgs);
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {unKey}\\{subKeyName}");
        }
    }
}
