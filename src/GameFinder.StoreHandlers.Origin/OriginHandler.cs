using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.Origin;

/// <summary>
/// Handler for finding games install with Origin.
/// </summary>
[PublicAPI]
public class OriginHandler : AHandler<OriginGame, OriginGameId>
{
    private readonly IRegistry? _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface. See the README for more information
    /// if you want to use Wine.
    /// </param>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <c>null</c>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    public OriginHandler(IFileSystem fileSystem, IRegistry? registry = null)
    {
        _fileSystem = fileSystem;
        _registry = registry;
    }

    internal static AbsolutePath GetManifestDir(IFileSystem fileSystem)
    {
        return fileSystem.GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("Origin")
            .Combine("LocalContent");
    }

    /// <inheritdoc/>
    public override Func<OriginGame, OriginGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<OriginGameId> IdEqualityComparer => OriginGameIdComparer.Default;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(@"SOFTWARE\Origin");
            if (regKey is null) return default;

            if (regKey.TryGetString("ClientPath", out var clientPath) && Path.IsPathFullyQualified(clientPath))
                return _fileSystem.FromUnsanitizedFullPath(clientPath);
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<OriginGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        var manifestDir = GetManifestDir(_fileSystem);

        if (!_fileSystem.DirectoryExists(manifestDir))
        {
            yield return new ErrorMessage($"Manifest folder {manifestDir} does not exist!");
            yield break;
        }

        var mfstFiles = _fileSystem.EnumerateFiles(manifestDir, "*.mfst").ToList();
        if (mfstFiles.Count == 0)
        {
            yield return new ErrorMessage($"Manifest folder {manifestDir} does not contain any .mfst files");
            yield break;
        }

        foreach (var mfstFile in mfstFiles)
        {
            var result = ParseMfstFile(mfstFile);

            // ignore steam games
            if (result.IsT2) continue;

            if (result.IsT1)
            {
                yield return result.AsT1;
                continue;
            }

            yield return result.AsT0;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    private OneOf<OriginGame, ErrorMessage, bool> ParseMfstFile(AbsolutePath filePath)
    {
        try
        {
            using var stream = _fileSystem.ReadFile(filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var contents = reader.ReadToEnd();

            var query = HttpUtility.ParseQueryString(contents, Encoding.UTF8);

            // using GetValues because some manifest have duplicate key-value entries for whatever reason
            var ids = query.GetValues("id");
            if (ids is null || ids.Length == 0)
            {
                return new ErrorMessage($"Manifest {filePath} does not have a value \"id\"");
            }

            var id = ids[0];
            if (id.EndsWith("@steam", StringComparison.OrdinalIgnoreCase))
                return true;

            var installPaths = query.GetValues("dipInstallPath");
            if (installPaths is null || installPaths.Length == 0)
            {
                return new ErrorMessage($"Manifest {filePath} does not have a value \"dipInstallPath\"");
            }

            var path = installPaths
                .OrderByDescending(x => x.Length)
                .First();

            var game = new OriginGame(
                Id: OriginGameId.From(id),
                InstallPath: Path.IsPathFullyQualified(path) ? _fileSystem.FromUnsanitizedFullPath(path) : new()
            );

            return game;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing {filePath}");
        }
    }
}
