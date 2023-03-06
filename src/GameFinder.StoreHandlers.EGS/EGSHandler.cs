using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;

namespace GameFinder.StoreHandlers.EGS;

/// <summary>
/// Represents a game installed with the Epic Games Store.
/// </summary>
/// <param name="CatalogItemId"></param>
/// <param name="DisplayName"></param>
/// <param name="InstallLocation"></param>
[PublicAPI]
public record EGSGame(string CatalogItemId, string DisplayName, string InstallLocation);

/// <summary>
/// Handler for finding games installed with the Epic Games Store.
/// </summary>
[PublicAPI]
public class EGSHandler : AHandler<EGSGame, string>
{
    internal const string RegKey = @"Software\Epic Games\EOS";
    internal const string ModSdkMetadataDir = "ModSdkMetadataDir";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

    private readonly JsonSerializerOptions _jsonSerializerOptions =
        new()
        {
            AllowTrailingCommas = true,
        };

    /// <summary>
    /// Default constructor. This uses the <see cref="WindowsRegistry"/> implementation of
    /// <see cref="IRegistry"/> and the real file system with <see cref="FileSystem"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public EGSHandler() : this(new WindowsRegistry(), new FileSystem()) { }

    /// <summary>
    /// Constructor for specifying the implementation of <see cref="IRegistry"/>. This uses
    /// the real file system with <see cref="FileSystem"/>.
    /// </summary>
    /// <param name="registry"></param>
    public EGSHandler(IRegistry registry) : this(registry, new FileSystem()) { }

    /// <summary>
    /// Constructor for specifying the implementation of <see cref="IRegistry"/> and
    /// <see cref="IFileSystem"/> when doing tests.
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="fileSystem"></param>
    public EGSHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override IEnumerable<Result<EGSGame>> FindAllGames()
    {
        var manifestDir = _fileSystem.DirectoryInfo.New(GetManifestDir());
        if (!manifestDir.Exists)
        {
            yield return Result.FromError<EGSGame>($"The manifest directory {manifestDir.FullName} does not exist!");
            yield break;
        }

        var itemFiles = manifestDir
            .EnumerateFiles("*.item", SearchOption.TopDirectoryOnly)
            .ToArray();

        if (itemFiles.Length == 0)
        {
            yield return Result.FromError<EGSGame>($"The manifest directory {manifestDir.FullName} does not contain any .item files");
            yield break;
        }

        foreach (var itemFile in itemFiles)
        {
            yield return DeserializeGame(itemFile);
        }
    }

    /// <inheritdoc/>
    public override IDictionary<string, EGSGame> FindAllGamesById(out string[] errors)
    {
        var (games, allErrors) = FindAllGames().SplitResults();
        errors = allErrors;

        return games.CustomToDictionary(game => game.CatalogItemId, game => game, StringComparer.OrdinalIgnoreCase);
    }

    private Result<EGSGame> DeserializeGame(IFileInfo itemFile)
    {
        using var stream = itemFile.OpenRead();

        try
        {
            var game = JsonSerializer.Deserialize<EGSGame>(stream, _jsonSerializerOptions);

            if (game is null)
            {
                return Result.FromError<EGSGame>($"Unable to deserialize file {itemFile.FullName}");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (game.CatalogItemId is null)
            {
                return Result.FromError<EGSGame>($"Manifest {itemFile.FullName} does not have a value \"CatalogItemId\"");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (game.DisplayName is null)
            {
                return Result.FromError<EGSGame>($"Manifest {itemFile.FullName} does not have a value \"DisplayName\"");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (game.InstallLocation is null)
            {
                return Result.FromError<EGSGame>($"Manifest {itemFile.FullName} does not have a value \"InstallLocation\"");
            }

            return Result.FromGame(game);
        }
        catch (Exception e)
        {
            return Result.FromError<EGSGame>($"Unable to deserialize file {itemFile.FullName}:\n{e}");
        }
    }

    private string GetManifestDir()
    {
        return TryGetManifestDirFromRegistry(out var manifestDir)
            ? manifestDir
            : GetDefaultManifestsPath(_fileSystem);
    }

    internal static string GetDefaultManifestsPath(IFileSystem fileSystem)
    {
        return fileSystem.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");
    }

    private bool TryGetManifestDirFromRegistry([MaybeNullWhen(false)] out string manifestDir)
    {
        manifestDir = default;

        try
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);
            using var regKey = currentUser.OpenSubKey(RegKey);

            return regKey is not null && regKey.TryGetString("ModSdkMetadataDir", out manifestDir);
        }
        catch (Exception)
        {
            return false;
        }
    }
}