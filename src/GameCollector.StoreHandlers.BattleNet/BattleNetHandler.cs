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
using ProtoBuf;

namespace GameCollector.StoreHandlers.BattleNet;

/// <summary>
/// Handler for finding games installed with Blizzard Battle.net.
/// </summary>
/// <remarks>
/// Uses Protobuf database:
///   %ProgramData%\Battle.net\Agent\product.db
/// and json files:
///   %ProgramData%\Battle.net\Agent\data\cache\??\??\*.
///   %AppData%\Battle.net\Battle.net.config
/// </remarks>
[PublicAPI]
public class BattleNetHandler : AHandler<BattleNetGame, BattleNetGameId>
{
    internal const string BattleNetRegKey = @"SOFTWARE\Blizzard Entertainment\Battle.net\Capabilities";

    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = SourceGenerationContext.Default,
        };

    private readonly IRegistry? _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface.
    /// </param>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    public BattleNetHandler(IFileSystem fileSystem, IRegistry? registry = null)
    {
        _fileSystem = fileSystem;
        _registry = registry;
    }

    /// <inheritdoc/>
    public override IEqualityComparer<BattleNetGameId>? IdEqualityComparer => BattleNetGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<BattleNetGame, BattleNetGameId> IdSelector => game => game.ProductId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(BattleNetRegKey);
            if (regKey is not null)
            {
                if (regKey.TryGetString("ApplicationIcon", out var appIcon))
                {
                    if (appIcon.Contains(',', StringComparison.Ordinal))
                        appIcon = appIcon[..appIcon.LastIndexOf(',')];
                    if (Path.IsPathFullyQualified(appIcon))
                        return _fileSystem.FromUnsanitizedFullPath(appIcon);
                }
            }
        }
        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<BattleNetGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var dataPath = GetBattleNetPath()
            .Combine("Agent").Combine("data").Combine("cache");
        if (!dataPath.DirectoryExists())
        {
            yield return new ErrorMessage($"The data directory {dataPath.GetFullPath()} does not exist!");
            yield break;
        }
        var dbFile = GetBattleNetPath().Combine("Agent").Combine("product.db");
        if (!dbFile.FileExists)
        {
            yield return new ErrorMessage($"The database file {dbFile.GetFullPath()} does not exist!");
            yield break;
        }
        var cfgFile = _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .Combine("Battle.net")
            .Combine("Battle.net.config");
        var uninstallExe = GetBattleNetPath()
            .Combine("Agent")
            .Combine("Blizzard Uninstaller.exe");

        var dataFiles = dataPath
            .EnumerateFiles(Extension.None, recursive: true)
            .ToArray();

        if (dataFiles.Length == 0)
        {
            yield return new ErrorMessage($"The data directory {dataPath.GetFullPath()} does not contain any cached files!");
            yield break;
        }

        foreach (var dataFile in dataFiles)
        {
            if (!dataFile.Extension.Equals(new Extension(".bmime")))
                yield return DeserializeGame(dataFile, dbFile, cfgFile, uninstallExe);
        }
    }

    /// <inheritdoc/>
    private OneOf<BattleNetGame, ErrorMessage> DeserializeGame(AbsolutePath dataFile, AbsolutePath dbFile, AbsolutePath cfgFile, AbsolutePath uninstallExe)
    {
        try
        {
            var (error, id, name, path, exe, description) = ParseDataFile(dataFile);
            if (error is not null)
                return new ErrorMessage(error);

            var (dbError, installPath, lang) = ParseDatabase(id, dbFile);
            if (dbError is not null)
                return new ErrorMessage(dbError);

            var lastRunDate = ParseConfigForLastRun(id, cfgFile);

            /*
            var (trName, trDescription) = ReparseForTranslations(lang, dataFile);
            if (!string.IsNullOrEmpty(trName))
                name = trName;
            if (!string.IsNullOrEmpty(trDescription))
                description = trDescription;
            */

            var uninstall = $"{uninstallExe}";
            if (!string.IsNullOrEmpty(path))
                installPath = Path.Combine(installPath, path);
            var launch = "";
            if (!string.IsNullOrEmpty(exe))
                launch = Path.Combine(installPath, exe);

            return new BattleNetGame(
                ProductId: BattleNetGameId.From(id),
                DirName: name,
                InstallPath: Path.IsPathFullyQualified(installPath) ? _fileSystem.FromUnsanitizedFullPath(installPath) : new(),
                BinaryPath: Path.IsPathFullyQualified(launch) ? _fileSystem.FromUnsanitizedFullPath(launch) : new(),
                Uninstaller: Path.IsPathFullyQualified(uninstall) ? _fileSystem.FromUnsanitizedFullPath(uninstall) : new(),
                UninstallArgs: $"--lang={lang} --uid={id} --displayname=\"{name}\"",
                LastPlayed: lastRunDate,
                AppDescription: description
            );
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Unable to deserialize file {dataFile.GetFullPath()}");
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private (string? error, string id, string name, string path, string exe, string description)
        ParseDataFile(AbsolutePath dataFile)
    {
        var id = "";
        var name = "";
        var path = "";
        var exe = "";
        var description = "";
        try
        {
            using var stream = dataFile.Read();
            var cache = JsonSerializer.Deserialize<CacheFile>(stream, JsonSerializerOptions);
            if (cache is null ||
                cache.All is null ||
                cache.All.Config is null ||
                cache.Platform is null ||
                cache.Platform.Win is null ||
                cache.Platform.Win.Config is null)
            {
                return ($"Unable to deserialize data file {dataFile.GetFullPath()}", "", "", "", "", "");
            }

            id = cache.All.Config.Product ?? "";
            path = cache.All.Config.SharedContainerDefaultSubfolder ?? "";
            if (string.IsNullOrEmpty(id) ||
                id.Equals("agent", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("bna", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("bts", StringComparison.OrdinalIgnoreCase))
            {
                return ($"Product \"{id}\" is not a game in file {dataFile.GetFullPath()}", "", "", "", "", "");
            }

            if (cache.All.Config.Form is not null &&
                cache.All.Config.Form.GameDir is not null)
            {
                name = cache.All.Config.Form.GameDir.Dirname ?? "";
            }
            if (string.IsNullOrEmpty(name) &&
                cache.Platform.Win.Config.Form is not null)
            {
                var form = cache.Platform.Win.Config.Form;
                if (form.GameDir is not null)
                    name = form.GameDir.Dirname ?? "";
            }
            if (string.IsNullOrEmpty(name))
                name = id;

            if (cache.Platform.Win.Config.Binaries is not null)
            {
                var bins = cache.Platform.Win.Config.Binaries;
                if (bins.Game is not null)
                {
                    exe = bins.Game.RelativePath64 ?? "";
                    if (string.IsNullOrEmpty(exe))
                        exe = bins.Game.RelativePath ?? "";
                }
            }

            if (string.IsNullOrEmpty(exe))
            {
                return ($"Data file {dataFile.GetFullPath()} does not have a value for \"relative_path\"", "", "", "", "", "");
            }

            if (cache.DefaultLanguage is not null &&
                cache.DefaultLanguage.Config is not null &&
                cache.DefaultLanguage.Config.Install is not null)
            {
                var installs = cache.DefaultLanguage.Config.Install;
                if (installs.Count > 0 &&
                    installs[0] is not null)
                {
                    var install = installs[0];
                    if (install.ProgramAssociations is not null)
                    {
                        description = install.ProgramAssociations.ApplicationDescription ?? "";
                    }
                }
            }
        }
        catch (Exception e)
        {
            return ($"Exception while deserializing file {dataFile.GetFullPath()}\n{e.Message}\n{e.InnerException}", "", "", "", "", "");
        }

        return (null, id, name, path, exe, description);
    }

    private static (string? error, string installPath, string lang)
        ParseDatabase(string id, AbsolutePath dbFile)
    {
        try
        {
            using var stream = dbFile.Read();
            var db = Serializer.Deserialize<BnetDatabase>(stream);

            foreach (var pi in db.productInstalls)
            {
                if (pi.productCode.Equals(id, StringComparison.OrdinalIgnoreCase) &&
                    pi.Settings is not null)
                {
                    var installPath = pi.Settings.installPath ?? "";
                    var lang = pi.Settings.selectedTextLanguage;
                    if (string.IsNullOrEmpty(lang))
                        lang = "enUS"; // default

                    return (null, installPath, lang);
                }
            }
        }
        catch (Exception)
        {
            return ($"Unable to deserialize database file: {dbFile.GetFullPath()}", "", "");
        }
        return ($"Unable to find productCode \"{id}\" in database file: {dbFile.GetFullPath()}", "", "");
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private DateTime ParseConfigForLastRun(string code, AbsolutePath cfgFile)
    {
        try
        {
            using var stream = cfgFile.Read();
            var config = JsonSerializer.Deserialize<ConfigFile>(stream, JsonSerializerOptions);
            if (config is not null &&
                config.Games.TryGetProperty(code, out var cfgGame))
            {
                var game = JsonSerializer.Deserialize<ConfigGame>(cfgGame, JsonSerializerOptions);
                if (game is not null &&
                    game.LastPlayed is not null &&
                    long.TryParse(game.LastPlayed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lLastRun))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(lLastRun).UtcDateTime;
                }
            }
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
        return DateTime.MinValue;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private (string name, string description) ReparseForTranslations(string lang, AbsolutePath dataFile)
    {
        try
        {
            using var stream = dataFile.Read();
            var root = JsonSerializer.Deserialize<JsonElement>(stream);
            root.TryGetProperty(lang.ToLower(CultureInfo.InvariantCulture), out var jLanguage);
            var cache = JsonSerializer.Deserialize<CacheFileConfig>(jLanguage);

            if (cache is not null &&
                cache.Config is not null &&
                cache.Config.Install is not null &&
                cache.Config.Install.Count > 0)
            {
                var install = cache.Config.Install[0];
                var name = "";
                var description = "";
                if (install.AddRemoveProgramsKey is not null)
                    name = install.AddRemoveProgramsKey.DisplayName ?? "";
                if (install.ProgramAssociations is not null)
                    description = install.ProgramAssociations.ApplicationDescription ?? "";
                return (name, description);
            }
        }
        catch (Exception) { }

        return new();
    }

    public AbsolutePath GetBattleNetPath()
    {
        return _fileSystem.GetKnownPath(KnownPath.CommonApplicationDataDirectory).Combine("Battle.net");
    }
}
