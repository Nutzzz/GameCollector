using System;
using System.Buffers;
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

namespace GameCollector.StoreHandlers.EGS;

/// <summary>
/// Handler for finding games installed with the Epic Games Store.
/// </summary>
[PublicAPI]
public partial class EGSHandler : AHandler<EGSGame, EGSGameId>
{
    internal const string RegKey = @"Software\Epic Games\EOS";
    internal const string ModSdkMetadataDir = "ModSdkMetadataDir";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.Strict,
        TypeInfoResolver = SourceGenerationContext.Default,
    };

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// The supported format version of this handler. You can change the format policy with
    /// <see cref="FormatPolicy"/>.
    /// </summary>
    public const int SupportedFormatVersion = 0;

    /// <summary>
    /// Policy to use when the format version does not match <see cref="SupportedFormatVersion"/>.
    /// The default behavior is <see cref="EGS.FormatPolicy.Warn"/>.
    /// </summary>
    public FormatPolicy FormatPolicy { get; set; } = FormatPolicy.Warn;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. For tests either use
    /// <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface. See the README for more information if you want to use
    /// Wine.
    /// </param>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface. See the README for more information
    /// if you want to use Wine.
    /// </param>
    public EGSHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override IEqualityComparer<EGSGameId> IdEqualityComparer => EGSGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<EGSGame, EGSGameId> IdSelector => game => game.CatalogItemId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(RegKey);
            if (regKey is null) return default;

            if (regKey.TryGetString("ModSdkCommand", out var command) && Path.IsPathFullyQualified(command))
                return _fileSystem.FromUnsanitizedFullPath(command);
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<EGSGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        List<OneOf<EGSGame, ErrorMessage>> allGames = [];
        var manifestDir = GetManifestDir();
        if (!_fileSystem.DirectoryExists(manifestDir))
        {
            allGames.Add(new ErrorMessage($"The manifest directory {manifestDir.GetFullPath()} does not exist!"));
            return allGames;
        }

        var itemFiles = _fileSystem
            .EnumerateFiles(manifestDir, "*.item")
            .ToArray();

        if (itemFiles.Length == 0)
        {
            allGames.Add(new ErrorMessage($"The manifest directory {manifestDir.GetFullPath()} does not contain any .item files"));
            return allGames;
        }

        Dictionary<EGSGameId, OneOf<EGSGame, ErrorMessage>> installedDict = [];
        foreach (var itemFile in itemFiles)
        {
            var game = DeserializeGame(itemFile, FormatPolicy, settings?.BaseOnly);
            if (game.IsT0)
            {
                try
                {
                    installedDict.Add(game.AsT0.CatalogItemId, game);
                }
                catch (Exception e)
                {
                    installedDict.TryAdd(EGSGameId.From(installedDict.Count.ToString()), new ErrorMessage(e, $"Exception adding \"{game.AsT0.GameName}\" [{game.AsT0.CatalogItemId}]"));
                }
                continue;
            }
            installedDict.TryAdd(EGSGameId.From(installedDict.Count.ToString()), game);
        }

        return GetOwnedGames(installedDict, _fileSystem, settings);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private OneOf<EGSGame, ErrorMessage> DeserializeGame(AbsolutePath itemFile, FormatPolicy formatPolicy, bool? baseOnly = false)
    {
        using var stream = _fileSystem.ReadFile(itemFile);

        try
        {
            var manifest = JsonSerializer.Deserialize<ManifestFile>(stream, JsonSerializerOptions);

            if (manifest is null)
            {
                return new ErrorMessage($"Unable to deserialize file {itemFile.GetFullPath()}");
            }

            var formatVersionNullable = manifest.FormatVersion;
            if (!formatVersionNullable.HasValue)
            {
                return new ErrorMessage($"Manifest {itemFile.GetFullPath()} does not have a value \"FormatVersion\"");
            }

            var formatVersion = formatVersionNullable.Value;
            var (formatMessage, isFormatError) = CreateFormatVersionMessage(formatPolicy, formatVersion, itemFile);
            if (formatMessage is not null)
            {
                if (isFormatError)
                    return new ErrorMessage(formatMessage);
            }

            var id = manifest.CatalogItemId ?? "";
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (string.IsNullOrEmpty(id))
            {
                return new ErrorMessage($"Manifest {itemFile.GetFullPath()} does not have a value \"CatalogItemId\"");
            }

            var title = manifest.DisplayName ?? "";
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (string.IsNullOrEmpty(title))
            {
                return new ErrorMessage($"[{id}] Manifest {itemFile.GetFullPath()} does not have a value \"DisplayName\"");
            }

            var loc = manifest.InstallLocation ?? "";
            if (string.IsNullOrEmpty(loc) || !Path.IsPathFullyQualified(loc))
            {
                return new ErrorMessage($"\"{title}\" [{id}] Manifest {itemFile.GetFullPath()} does not have a value \"InstallLocation\"");
            }

            var isDLC = false;
            var exe = manifest.LaunchExecutable;
            AbsolutePath launch = new();
            if (string.IsNullOrEmpty(exe))
            {
                if (baseOnly == true)
                    return new ErrorMessage($"\"{title}\" [{id}] Manifest {itemFile.GetFullPath()} is a DLC or has no LaunchExecutable");
                isDLC = true;
            }
            else
                launch = _fileSystem.FromUnsanitizedFullPath(loc).Combine(exe);

            var game = new EGSGame(
                CatalogItemId: EGSGameId.From(id),
                DisplayName: title,
                InstallLocation: _fileSystem.FromUnsanitizedFullPath(loc),
                CloudSaveFolder: new(),
                InstallLaunch: launch,
                IsInstalled: true,
                MainGame: isDLC ? (!isDLC).ToString() : null);

            return game;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Unable to deserialize file {itemFile.GetFullPath()}");
        }
    }

    internal static (string? message, bool isError) CreateFormatVersionMessage(
        FormatPolicy formatPolicy, int formatVersion, AbsolutePath manifestFilePath)
    {
        if (formatVersion == SupportedFormatVersion) return (null, false);

        return formatPolicy switch
        {
            FormatPolicy.Warn => (
                $"Manifest {manifestFilePath} has a FormatVersion " +
                $"{formatVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports FormatVersion " +
                $"{SupportedFormatVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This message is a WARNING because the consumer of this library has set {nameof(FormatPolicy)} to {nameof(FormatPolicy.Warn)}",
                false),
            FormatPolicy.Error => (
                $"Manifest {manifestFilePath} has a FormatVersion " +
                $"{formatVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports FormatVersion " +
                $"{SupportedFormatVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This is an ERROR because the consumer of this library has set {nameof(FormatPolicy)} to {nameof(FormatPolicy.Error)}",
                true),
            FormatPolicy.Ignore => (null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(formatPolicy), formatPolicy, message: null),
        };
    }

    private AbsolutePath GetManifestDir()
    {
        return TryGetManifestDirFromRegistry(out var manifestDir)
            ? manifestDir
            : GetDefaultManifestsPath(_fileSystem);
    }

    internal static AbsolutePath GetDefaultManifestsPath(IFileSystem fileSystem)
    {
        return fileSystem
            .GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("Epic/EpicGamesLauncher/Data/Manifests");
    }

    private bool TryGetManifestDirFromRegistry(out AbsolutePath manifestDir)
    {
        manifestDir = default;

        try
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);
            using var regKey = currentUser.OpenSubKey(RegKey);

            if (regKey is null || !regKey.TryGetString("ModSdkMetadataDir",
                    out var registryMetadataDir)) return false;

            manifestDir = _fileSystem.FromUnsanitizedFullPath(registryMetadataDir);
            return true;

        }
        catch (Exception)
        {
            return false;
        }
    }
}
