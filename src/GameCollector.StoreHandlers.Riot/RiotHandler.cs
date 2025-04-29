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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GameCollector.StoreHandlers.Riot;

/// <summary>
/// Handler for finding games installed with the Riot Client.
/// </summary>
/// <remarks>
/// Constructor. Uses json file:
///   %ProgramData%\Riot Games\RiotClientInstalls.json
/// and yaml files:
///   %ProgramData%\Riot Games\Metadata\*\*settings.yaml
/// </remarks>
/// <param name="fileSystem">
/// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
/// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
/// a custom implementation or just a mock of the interface.
/// </param>
/// <param name="registry">
/// The implementation of <see cref="IRegistry"/> to use. For a shared instance
/// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <langword>null</langword>.
/// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
/// of the interface.
/// </param>
[PublicAPI]
public class RiotHandler(IFileSystem fileSystem, IRegistry? registry = null) : AHandler<RiotGame, RiotGameId>
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

    private readonly IRegistry? _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc/>
    public override IEqualityComparer<RiotGameId>? IdEqualityComparer => RiotGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<RiotGame, RiotGameId> IdSelector => game => game.ProductId;

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override AbsolutePath FindClient()
    {
        try
        {
            // could also get client path from HKEY_CLASSES_ROOT\riotclient\shell\open\command\(Default)
            // or just use protocol "riotclient://"

            var clientFile = GetRiotPath().Combine("RiotClientInstalls.json");

            using var stream = clientFile.Read();
            var client = JsonSerializer.Deserialize<ClientInstallFile>(stream, JsonSerializerOptions);
            if (client is not null && !string.IsNullOrEmpty(client.RcLive))
            {
                var clientPath = client.RcLive;
                if (Path.IsPathRooted(clientPath))
                    return _fileSystem.FromUnsanitizedFullPath(clientPath);
            }
        }
        catch (Exception) { }

        return default;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public override IEnumerable<OneOf<RiotGame, ErrorMessage>> FindAllGames(GameFinder.Common.Settings? settings = null)
    {
        var clientFile = GetRiotPath().Combine("RiotClientInstalls.json");
        var metaDir = GetRiotPath().Combine("Metadata");
        if (!clientFile.FileExists)
        {
            yield return new ErrorMessage($"The client install file {clientFile.GetFullPath()} does not exist!");
            yield break;
        }

        // could also get client path from HKEY_CLASSES_ROOT\riotclient\shell\open\command\(Default) though I'm not sure how to use "--app-command=" properly
        var clientPath = "";

        using var stream = clientFile.Read();
        var client = JsonSerializer.Deserialize<ClientInstallFile>(stream, JsonSerializerOptions);
        if (client is not null && client.RcLive is not null)
            clientPath = client.RcLive;

        var settingsFiles = metaDir
            .EnumerateFiles("*settings.yaml", recursive: true)
            .ToArray();

        if (settingsFiles.Length == 0)
        {
            yield return new ErrorMessage($"The metadata directory {metaDir.GetFullPath()} does not contain any .yaml files");
            yield break;
        }

        foreach (var file in settingsFiles)
        {
            yield return DeserializeGame(file, clientPath);
        }
    }

    private OneOf<RiotGame, ErrorMessage> DeserializeGame(AbsolutePath settingsFile, string clientPath)
    {
        using var stream = settingsFile.Read();

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var game = deserializer.Deserialize<SettingsFile>(new StreamReader(stream).ReadToEnd());

            if (game is null)
            {
                return new ErrorMessage($"Unable to deserialize file {settingsFile.GetFullPath()}");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (game.UserDataPaths is not null) // This should only be true for "Riot Client.settings.yaml"
            {
                return new ErrorMessage($"Not a game file {settingsFile.GetFullPath()}");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (game.ProductInstallFullPath is null || game.ProductInstallRoot is null)
            {
                return new ErrorMessage($"No \"product_install_full_path\" property in file {settingsFile.GetFullPath()}");
            }

            var name = game.ShortcutName ?? settingsFile.FileName[..settingsFile.FileName.IndexOf('.', StringComparison.Ordinal)];

            var id = game.ProductInstallFullPath[(game.ProductInstallRoot.Length + 1)..];
            if (id.Contains('/', StringComparison.Ordinal))
                id = id[..id.IndexOf('/', StringComparison.Ordinal)];

            var product = Path.GetFileNameWithoutExtension(settingsFile.FileName);
            if (product.Contains('.', StringComparison.Ordinal))
                product = product[..product.IndexOf('.', StringComparison.Ordinal)].ToLower(CultureInfo.InvariantCulture);

            var launch = clientPath;
            var launchArgs = "--launch-product=" + product;
            var uninstallArgs = "--uninstall-product=" + product;
            if (settingsFile.FileName.Contains(".live.", StringComparison.OrdinalIgnoreCase))
            {
                launchArgs += " --launch-patchline=live";
                uninstallArgs += " --uninstall-patchline=live";
            }

            AbsolutePath icon = new();
            if (settingsFile.Directory is not null)
            {
                foreach (var iconFile in _fileSystem.EnumerateFiles(
                    _fileSystem.FromUnsanitizedFullPath(settingsFile.Directory),
                    "*.ico",
                    recursive: false))
                {
                    icon = iconFile;
                    break;
                }
            }
            if (string.IsNullOrEmpty(icon.GetFullPath()))
                icon = _fileSystem.FromUnsanitizedFullPath(launch);

            return new RiotGame(
                ProductId: RiotGameId.From(id),
                Name: Path.GetFileNameWithoutExtension(game.ShortcutName ?? ""),
                ProductInstallPath: Path.IsPathRooted(game.ProductInstallFullPath) ? _fileSystem.FromUnsanitizedFullPath(game.ProductInstallFullPath) : new(),
                ClientPath: _fileSystem.FromUnsanitizedFullPath(launch),
                LaunchArgs: launchArgs,
                Icon: icon,
                UninstallArgs: uninstallArgs);
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Unable to deserialize file {settingsFile.GetFullPath()}");
        }
    }

    public AbsolutePath GetRiotPath()
    {
        return _fileSystem.GetKnownPath(KnownPath.CommonApplicationDataDirectory)
                .Combine("Riot Games");
    }
}
