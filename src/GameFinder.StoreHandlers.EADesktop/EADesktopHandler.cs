using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.EADesktop.Crypto;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.EADesktop;

/// <summary>
/// Handler for finding games installed with EA Desktop.
/// </summary>
[PublicAPI]
public class EADesktopHandler : AHandler<EADesktopGame, EADesktopGameId>
{
    internal const string AllUsersFolderName = "530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e";
    internal const string InstallInfoFileName = "IS";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.Strict,
        TypeInfoResolver = SourceGenerationContext.Default,
    };

    private readonly IFileSystem _fileSystem;
    private readonly IRegistry _registry;
    private readonly IHardwareInfoProvider _hardwareInfoProvider;

    /// <summary>
    /// The supported schema version of this handler. You can change the schema policy with
    /// <see cref="SchemaPolicy"/>.
    /// </summary>
    public const int SupportedSchemaVersion = 21;

    /// <summary>
    /// Policy to use when the schema version does not match <see cref="SupportedSchemaVersion"/>.
    /// The default behavior is <see cref="EADesktop.SchemaPolicy.Warn"/>.
    /// </summary>
    public SchemaPolicy SchemaPolicy { get; set; } = SchemaPolicy.Warn;

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
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <c>null</c>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    /// <param name="hardwareInfoProvider">
    /// The implementation of <see cref="IHardwareInfoProvider"/> to use. Currently only
    /// <see cref="GameCollector.StoreHandlers.EADesktop.Crypto.Windows.HardwareInfoProvider"/>
    /// is available and is Windows-only.
    /// </param>
    public EADesktopHandler(IFileSystem fileSystem, IRegistry registry, IHardwareInfoProvider hardwareInfoProvider)
    {
        _fileSystem = fileSystem;
        _registry = registry;
        _hardwareInfoProvider = hardwareInfoProvider;
    }

    /// <inheritdoc/>
    public override IEqualityComparer<EADesktopGameId> IdEqualityComparer => EADesktopGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<EADesktopGame, EADesktopGameId> IdSelector => game => game.EADesktopGameId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine = _registry.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

            using var regKey = localMachine.OpenSubKey(@"SOFTWARE\Electronic Arts\EA Desktop");
            if (regKey is null) return default;

            if (regKey.TryGetString("LauncherAppPath", out var path) && Path.IsPathFullyQualified(path))
                return _fileSystem.FromUnsanitizedFullPath(path);
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<EADesktopGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var dataFolder = GetDataFolder(_fileSystem);
        if (!_fileSystem.DirectoryExists(dataFolder))
        {
            yield return new ErrorMessage($"Data folder {dataFolder} does not exist!");
            yield break;
        }

        var installInfoFile = GetInstallInfoFile(dataFolder);
        if (!_fileSystem.FileExists(installInfoFile))
        {
            yield return new ErrorMessage($"File does not exist: {installInfoFile.GetFullPath()}");
            yield break;
        }

        var decryptionResult = DecryptInstallInfoFile(_fileSystem, installInfoFile, _hardwareInfoProvider);
        if (decryptionResult.TryGetError(out var error))
        {
            yield return error;
            yield break;
        }

        var plaintext = decryptionResult.AsT0;
        foreach (var result in ParseInstallInfoFile(plaintext, installInfoFile, SchemaPolicy, settings))
        {
            yield return result;
        }
    }

    internal static AbsolutePath GetDataFolder(IFileSystem fileSystem)
    {
        return fileSystem
            .GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("EA Desktop");
    }

    internal static AbsolutePath GetInstallInfoFile(AbsolutePath dataFolder)
    {
        return dataFolder
            .Combine(AllUsersFolderName)
            .Combine(InstallInfoFileName);
    }

    internal static OneOf<string, ErrorMessage> DecryptInstallInfoFile(IFileSystem fileSystem, AbsolutePath installInfoFile, IHardwareInfoProvider hardwareInfoProvider)
    {
        try
        {
            using var stream = fileSystem.ReadFile(installInfoFile);
            var cipherText = new byte[stream.Length];
            var unused = stream.Read(cipherText);

            var key = Decryption.CreateDecryptionKey(hardwareInfoProvider);

            var iv = Decryption.CreateDecryptionIV();
            var plainText = Decryption.DecryptFile(cipherText, key, iv);
            return plainText;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while decrypting file {installInfoFile.GetFullPath()}");
        }
    }

    internal IEnumerable<OneOf<EADesktopGame, ErrorMessage>> ParseInstallInfoFile(string plaintext, AbsolutePath installInfoFile, SchemaPolicy schemaPolicy, Settings? settings = null)
    {
        try
        {
            return ParseInstallInfoFileInner(plaintext, installInfoFile, schemaPolicy, settings);
        }
        catch (Exception e)
        {
            return new OneOf<EADesktopGame, ErrorMessage>[]
            {
                new ErrorMessage(e, $"Exception while parsing InstallInfoFile {installInfoFile.GetFullPath()}"),
            };
        }
    }


    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private IEnumerable<OneOf<EADesktopGame, ErrorMessage>> ParseInstallInfoFileInner(string plaintext, AbsolutePath installInfoFile, SchemaPolicy schemaPolicy, Settings? settings = null)
    {
        var installInfoFileContents = JsonSerializer.Deserialize<InstallInfoFile>(plaintext, JsonSerializerOptions);

        if (installInfoFileContents is null)
        {
            yield return new ErrorMessage($"Unable to deserialize InstallInfoFile {installInfoFile.GetFullPath()}");
            yield break;
        }

        var schemaVersionNullable = installInfoFileContents.Schema?.Version;
        if (!schemaVersionNullable.HasValue)
        {
            yield return new ErrorMessage($"InstallInfoFile {installInfoFile.GetFullPath()} does not have a schema version!");
            yield break;
        }

        var schemaVersion = schemaVersionNullable.Value;
        var (schemaMessage, isSchemaError) = CreateSchemaVersionMessage(schemaPolicy, schemaVersion, installInfoFile);
        if (schemaMessage is not null)
        {
            yield return new ErrorMessage(schemaMessage);
            if (isSchemaError) yield break;
        }

        var installInfos = installInfoFileContents.InstallInfos;
        if (installInfos is null || installInfos.Count == 0)
        {
            yield return new ErrorMessage($"InstallInfoFile {installInfoFile.GetFullPath()} does not have any infos!");
            yield break;
        }

        for (var i = 0; i < installInfos.Count; i++)
        {
            yield return InstallInfoToGame(_registry, _fileSystem, installInfos[i], i, installInfoFile, settings);
        }
    }

    internal static (string? message, bool isError) CreateSchemaVersionMessage(
        SchemaPolicy schemaPolicy, int schemaVersion, AbsolutePath installInfoFilePath)
    {
        if (schemaVersion == SupportedSchemaVersion) return (null, false);

        return schemaPolicy switch
        {
            SchemaPolicy.Warn => (
                $"InstallInfoFile {installInfoFilePath} has a schema version " +
                $"{schemaVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports schema version " +
                $"{SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This message is a WARNING because the consumer of this library has set {nameof(SchemaPolicy)} to {nameof(SchemaPolicy.Warn)}",
                false),
            SchemaPolicy.Error => (
                $"InstallInfoFile {installInfoFilePath} has a schema version " +
                $"{schemaVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports schema version " +
                $"{SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This is an ERROR because the consumer of this library has set {nameof(SchemaPolicy)} to {nameof(SchemaPolicy.Error)}",
                true),
            SchemaPolicy.Ignore => (null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaPolicy), schemaPolicy, message: null),
        };
    }

    [RequiresUnreferencedCode("Calls System.Xml.Serialization.XmlSerializer.XmlSerializer(Type)")]
    internal static string ParseInstallerDataFile(IFileSystem fileSystem, string baseInstallPath, out IList<string> contentIds)
    {
        var title = "";
        contentIds = [];

        var dataFile = fileSystem.FromUnsanitizedFullPath(Path.Combine(baseInstallPath, "__Installer", "installerdata.xml"));
        if (dataFile.FileExists)
        {
            try
            {
                XmlSerializer dataSerializer = new(typeof(InstallerDataManifest));
                using var stream = dataFile.Read();
                var dataMnfst = (InstallerDataManifest?)dataSerializer.Deserialize(stream);

                if (dataMnfst is not null)
                {
                    dataMnfst.ContentIds?.ForEach(contentIds.Add);

                    foreach (var gameTitle in dataMnfst.GameTitles)
                    {
                        /*
                        if (gameTitle.TitleLocale.Equals("en_US", StringComparison.OrdinalIgnoreCase))
                        {
                        */
                        title = gameTitle.TitleText ?? "";
                        break;
                        //}
                        //else { }
                    }
                }
            }
            catch (Exception) { }

            try
            {
                XmlSerializer dataSerializer2 = new(typeof(InstallerDataGame));
                using var stream2 = dataFile.Read();
                var dataGame = (InstallerDataGame?)dataSerializer2.Deserialize(stream2);

                if (dataGame is not null)
                {
                    dataGame.ContentIds?.ForEach(contentIds.Add);

                    if (dataGame.Metadata is not null && dataGame.Metadata.LocaleInfo is not null)
                    {
                        var info = dataGame.Metadata.LocaleInfo;
                        /*
                        if (info.InfoLocale is not null &&
                            info.InfoLocale.Equals("en_US", StringComparison.OrdinalIgnoreCase))
                        {
                        */
                        title = info.InfoTitle ?? "";
                        //}
                        //else { }
                    }
                }
            }
            catch (Exception) { }
        }

        return title;
    }

    [RequiresUnreferencedCode("Calls GameCollector.StoreHandlers.EADesktop.EADesktopHandler.ParseInstallerDataFile(IFileSystem, String, out IList<String>)")]
    internal static OneOf<EADesktopGame, ErrorMessage> InstallInfoToGame(IRegistry registry, IFileSystem fileSystem, InstallInfo installInfo, int i, AbsolutePath installInfoFilePath, Settings? settings = null)
    {
        var isInstalled = true;
        var num = i.ToString(CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(installInfo.SoftwareId))
        {
            return new ErrorMessage($"InstallInfo #{num} does not have the value \"softwareId\"");
        }

        var softwareId = installInfo.SoftwareId;
        var isDLC = false;
        var baseSlug = installInfo.BaseSlug ?? softwareId;

        // only catches some DLC
        if (!string.IsNullOrEmpty(installInfo.DLCSubPath))
        {
            if (settings?.BaseOnly == true)
                return new ErrorMessage($"InstallInfo #{num} for \"{baseSlug}\" is a DLC");
            isDLC = true;
        }

        if (string.IsNullOrEmpty(installInfo.BaseInstallPath))
        {
            if (settings?.InstalledOnly == true)
                return new ErrorMessage($"InstallInfo #{num} for \"{baseSlug}\" does not have the value \"baseInstallPath\"");
            isInstalled = false;
        }

        if (string.IsNullOrEmpty(installInfo.ExecutableCheck) && string.IsNullOrEmpty(installInfo.ExecutablePath))
        {
            if (settings?.InstalledOnly == true)
                return new ErrorMessage($"InstallInfo #{num} for \"{baseSlug}\" does not have the value \"executableCheck\" or \"executablePath\"");
            isInstalled = false;
        }

        // executablePath only in newer versions of EA app
        var executablePath = installInfo.ExecutablePath ?? "";
        var baseInstallPath = installInfo.BaseInstallPath ?? "";
        var installCheck = installInfo.InstallCheck ?? "";
        var executableCheck = installInfo.ExecutableCheck ?? "";

        var sInstRegKey = "";
        var title = "";
        var pub = "";
        var sExeRegKey = "";
        AbsolutePath executable = new();

        if (Path.IsPathFullyQualified(executablePath))
            isInstalled = true;
        else if (Path.IsPathFullyQualified(baseInstallPath))
            isInstalled = true;

        var sInstFile = "";
        if (installCheck.StartsWith('['))
        {
            var j = installCheck.IndexOf(']', StringComparison.Ordinal);
            if (j > 1)
                sInstRegKey = installCheck[1..j];
            var install = fileSystem.FromUnsanitizedFullPath(baseInstallPath)
                .Combine(RelativePath.FromUnsanitizedInput(installCheck.AsSpan()[(j + 1)..]));
            if (!install.FileExists)
                isInstalled = false;

            var k = sInstRegKey.IndexOf("\\Install Dir", StringComparison.Ordinal);

            if (k > 0)
            {
                title = Path.GetFileName(sInstRegKey[..k]);
                var l = sInstRegKey.IndexOf(title, StringComparison.Ordinal);
                pub = Path.GetFileName(sInstRegKey[..l].TrimEnd('/', '\\'));
            }

            // TODO: Do I need this?
            sInstFile = installCheck[(j + 1)..];
        }

        if (isInstalled)
        {
            if (!string.IsNullOrEmpty(executablePath) && Path.IsPathFullyQualified(executablePath))
            {
                executable = fileSystem.FromUnsanitizedFullPath(executablePath);
                if (!executable.FileExists)
                    isInstalled = false;
            }
            else if (!string.IsNullOrEmpty(executableCheck) && executableCheck.StartsWith('['))
            {
                var j = executableCheck.IndexOf(']', StringComparison.Ordinal);

                // only catches some DLC
                if (j == 1)
                {
                    if (settings?.BaseOnly == true)
                        return new ErrorMessage($"InstallInfo #{num} for \"{baseSlug}\" is a DLC");
                    isDLC = true;
                }
                else if (j > 1)
                    sExeRegKey = executableCheck[1..j];
                executable = fileSystem.FromUnsanitizedFullPath(baseInstallPath).Combine(executableCheck[(j + 1)..]);
                if (!executable.FileExists)
                    isInstalled = false;
            }
        }

        var uninstall = "";
        var uninstallArgs = "";
        var localUninstallProperties = installInfo.LocalUninstallProperties;
        if (localUninstallProperties != null)
        {
            uninstall = localUninstallProperties.UninstallCommand ?? "";
            uninstallArgs = localUninstallProperties.UninstallParameters ?? "";
        }

        if (!string.IsNullOrEmpty(sExeRegKey))
        {
            try
            {
                var k = sExeRegKey.IndexOf('\\', StringComparison.Ordinal);
                var l = sExeRegKey.LastIndexOf('\\');
                if (k > 1 && l > k)
                {
                    var regRoot = registry.OpenBaseKey(RegistryHelpers.RegistryHiveFromString(sExeRegKey[..k].ToUpperInvariant()), RegistryView.Registry32);
                    var sSubKey = sExeRegKey[(k + 1)..l];
                    var subKey = regRoot.OpenSubKey(sSubKey);
                    subKey?.TryGetString("DisplayName", out title);
                }
            }
            catch (Exception) { }
        }

        var dataTitle = ParseInstallerDataFile(fileSystem, baseInstallPath, out var contentIds);
        if (string.IsNullOrEmpty(title))
        {
            if (!string.IsNullOrEmpty(dataTitle))
                title = dataTitle;
            else if (!string.IsNullOrEmpty(baseSlug))
            {
                CultureInfo ci = new("en-US");
                var ti = ci.TextInfo;
                title = ti.ToTitleCase(baseSlug.Replace('-', ' '));
            }
            else
                title = Path.GetFileName(baseInstallPath.TrimEnd('\\', '/'));
        }

        var game = new EADesktopGame(
            EADesktopGameId: EADesktopGameId.From(softwareId),
            Name: title,
            BaseInstallPath: Path.IsPathFullyQualified(baseInstallPath) ? fileSystem.FromUnsanitizedFullPath(baseInstallPath) : new(),
            Executable: executable,
            UninstallCommand: Path.IsPathFullyQualified(uninstall) ? fileSystem.FromUnsanitizedFullPath(uninstall) : new(),
            UninstallParameters: uninstallArgs,
            IsInstalled: isInstalled,
            IsDLC: isDLC,
            Publisher: pub,
            BaseSlug: baseSlug,
            ContentIDs: contentIds);

        return game;
    }
}
