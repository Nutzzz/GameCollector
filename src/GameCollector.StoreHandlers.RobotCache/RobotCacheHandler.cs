using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.RobotCache;

/// <summary>
/// Handler for finding games installed with the Robot Cache Client.
/// </summary>
/// <remarks>
/// Constructor. Uses json file:
///   %AppDataLocal%\RobotCache\RobotCacheClient\config\appConfig.json
/// </remarks>
/// <param name="fileSystem">
/// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
/// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
/// a custom implementation or just a mock of the interface.
/// </param>
[PublicAPI]
public class RobotCacheHandler(IFileSystem fileSystem) : AHandler<RobotCacheGame, RobotCacheGameId>
{
    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = SourceGenerationContext.Default,
        };

    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override Func<RobotCacheGame, RobotCacheGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<RobotCacheGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override AbsolutePath FindClient()
    {
        try
        {
            // TODO: could get client path from HKEY_CLASSES_ROOT\RobotCache\shell\open\command\(Default)
            // or just use protocol "RobotCache://"
        }
        catch (Exception) { }

        return default;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override IEnumerable<OneOf<RobotCacheGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var configFile = _fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .Combine("RobotCache")
            .Combine("RobotCacheClient")
            .Combine("config")
            .Combine("appConfig.json");
        if (!configFile.FileExists)
        {
            yield return new ErrorMessage($"The file {configFile.GetFullPath()} does not exist!");
            yield break;
        }

        List<string> libPaths = [];
        List<AbsolutePath> infoFiles = [];

        using var stream = configFile.Read();
        var config = JsonSerializer.Deserialize<AppConfigFile>(stream, JsonSerializerOptions);

        // TODO: Verify FormatVersions

        if (config is not null && config.Libraries is not null)
            libPaths = config.Libraries;

        foreach (var libPath in libPaths)
        {
            if (Path.IsPathFullyQualified(libPath))
            {
                infoFiles = _fileSystem.FromUnsanitizedFullPath(libPath).Combine("rcdata").Combine("games")
                    .EnumerateFiles("stateInfo.json", recursive: true)
                    .ToList();
            }
        }

        if (infoFiles.Count == 0)
        {
            yield return new ErrorMessage($"The game libraries do not contain any stateInfo.json files");
            yield break;
        }

        foreach (var file in infoFiles)
        {
            yield return DeserializeGame(file);
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private OneOf<RobotCacheGame, ErrorMessage> DeserializeGame(AbsolutePath infoFile)
    {
        using var stream = infoFile.Read();
        var info = JsonSerializer.Deserialize<StateInfoFile>(stream, JsonSerializerOptions);

        // TODO: Verify FormatVersions

        if (info is null || info.Execution is null)
        {
            return new ErrorMessage($"Unable to deserialize file {infoFile.GetFullPath()}");
        }

        var id = info.GameId ?? 0;
        var title = "";
        var exePath = "";
        AbsolutePath instPath = new();
        var launchParams = "";
        AbsolutePath icon = new();
        foreach (var exe in info.Execution)
        {
            title = exe.Title ?? "";
            exePath = exe.Path ?? "";
            var dir = Path.GetFileName(infoFile.Directory);
            instPath = infoFile.Parent.Parent.Parent.Parent.Combine("library").Combine(dir);
            launchParams = exe.Params ?? "";
            break;
        }

        try
        {
            foreach (var iconFile in _fileSystem.EnumerateFiles(infoFile.Parent.Parent.Parent.Combine("icons"), $"{id.ToString(CultureInfo.InvariantCulture)}*.ico", recursive: false))
            {
                icon = iconFile;
                break;
            }
        }
        catch (Exception) { }

        return new RobotCacheGame(
            Id: RobotCacheGameId.From(id),
            Title: title,
            InstallPath: instPath,
            ExecutionPath: exePath,
            ExecutionParams: launchParams,
            Icon: icon);
    }
}
