using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;

//using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameCollector.Common;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusMods.Paths;
using OneOf;
using OneOf.Types;

namespace GameCollector.PkgHandlers.Scoop;

/// <summary>
/// Handler for finding installed apps via Scoop.
/// </summary>
/// <remarks>
/// Constructor. Leverages scoop, which inspects local scoop database for installed programs.
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
/// <param name="logger">Logger.</param>
[PublicAPI]
public class ScoopHandler(IRegistry registry, IFileSystem fileSystem, ILogger? logger = null) : AHandler<ScoopGame, ScoopGameId>
{
    internal const string DefaultQuery = "game";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger _logger = logger ?? NullLogger<ScoopHandler>.Instance;

    private readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = SourceGenerationContext.Default,
        };

    private readonly ProcessStartInfo _startInfo = new()
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true,
    };

    /// <inheritdoc/>
    public override IEqualityComparer<ScoopGameId>? IdEqualityComparer => ScoopGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<ScoopGame, ScoopGameId> IdSelector => game => game.Name;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        AbsolutePath scoopCmd;
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';').ToList();
        if (paths is not null && paths.Count > 0)
        {
            foreach (var path in paths)
            {
                if (path.TrimEnd('\\').EndsWith(Path.Combine("scoop", "shims"), StringComparison.OrdinalIgnoreCase))
                {
                    scoopCmd = _fileSystem.FromUnsanitizedFullPath(Path.Combine(path, "scoop.cmd"));
                    if (scoopCmd.FileExists)
                        return scoopCmd;
                    break;
                }
            }
        }
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        scoopCmd = _fileSystem.FromUnsanitizedFullPath(Path.Combine(userProfile, "scoop", "shims", "scoop.cmd"));
        if (Path.IsPathFullyQualified(userProfile) && scoopCmd.FileExists)
            return scoopCmd;

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<ScoopGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        return FindAllGames(settings?.InstalledOnly ?? false, settings?.OwnedOnly ?? false, settings?.GamesOnly ?? false);
    }

    /// <summary>
    /// Finds all apps supported by this package handler. The return type
    /// will always be a non-null game or a non-null error message.
    /// </summary>
    /// <param name="installedOnly"></param>
    /// <param name="ownedOnly"></param>
    /// <param name="gamesOnly"></param>
    /// <param name="query"></param>
    /// <param name="expandPackage">Get additional information about scoop/msstore packages</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    public IEnumerable<OneOf<ScoopGame, ErrorMessage>> FindAllGames(
        bool installedOnly = false,
        bool ownedOnly = false,
        bool gamesOnly = false,
        string? query = DefaultQuery,
        bool expandPackage = false)
    {
        Dictionary<ScoopGameId, OneOf<ScoopGame, ErrorMessage>> installed = [];

        // "scoop export" = json: bucket, game, and [optionally] config data
        // "scoop cat <name>" = json: misc info
        // "scoop info <name>" = table w/colons: info of online package
        // "scoop prefix <name>" = oneliner: install directory
        // "scoop search <name>" = table w/colons: find packages online
        // "scoop which <name>" = oneliner: bin and location

        var scoopCmd = FindClient();
        if (scoopCmd == default)
        {
            _logger?.LogDebug("***Scoop not installed");
            yield return new ErrorMessage("Scoop not installed");
        }

        var foundGameBucket = false;
        var foundMultisource = false;

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = scoopCmd.GetFullPath();
        process.StartInfo.Arguments = "export --config";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var export = JsonSerializer.Deserialize<Export>(output, JsonSerializerOptions);

        if (export?.Buckets is not null)
        {
            foreach (var bucket in export.Buckets)
            {
                var name = bucket.Name ?? "";
                if (name.Equals("games", StringComparison.OrdinalIgnoreCase))
                {
                    foundGameBucket = true;
                }
                if (name.Equals(".sm", StringComparison.OrdinalIgnoreCase))
                {
                    foundMultisource = true;
                }
            }
        }
        if (!foundMultisource)
        {
            // Install scoop-search-multisource (allows searching descriptions)
            process.StartInfo.Arguments = "bucket add .ssms https://github.com/plicit/scoop-search-multisource.git";
            process.Start();
            _ = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            process.StartInfo.Arguments = "install scoop-search-multisource";
            process.Start();
            _ = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }
        if (!foundGameBucket)
        {
            process.StartInfo.Arguments = "bucket add games";
            process.Start();
            _ = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        if (export?.Apps is not null)
        {
            foreach (var app in export.Apps)
            {
                var name = app.Name ?? "";
                if (string.IsNullOrEmpty(name))
                    continue;
                var id = ScoopGameId.From(name);
                //var strBin = app.Binaries.Split('|')[0];
                //if (!string.IsNullOrEmpty(strBin))
                //    bin = _fileSystem.FromUnsanitizedFullPath(strBin.Trim());
                //installInfo = 
                installed.Add(id, new ScoopGame(
                    Name: id,
                    ShortcutName: name, // TODO: ???
                    Prefix: default, //$"~/scoop/apps/{name}/current",
                    Binary: default,
                    IsInstalled: true,
                    UpdateDate: Utils.ToNullableDateTimeInvariant(app.Updated),
                    //Source: app.Source,
                    InstallVersion: app.Version,
                    InstallInfo: app.Info,
                    Problems: ParseProblems("") // TODO: ???
                ));
            }
        }


        //return gameDict;

        //if (export.SiteData is null || export.SiteData.Catalog is null)
        //    return gameDict;


        if (!ownedOnly)
            installed = GetInstalled(expandPackage);

        /*
        foreach (var catalog in availableCatalogs.ToArray())
        {
            foreach (var game in SearchPackages(query, expandPackage))
            {
                var pkg = match.CatalogPackage;
                var name = ScoopGameId.From(pkg.Name);

                installed.Add(id, new ScoopGame(
                    Name: pkg.Name,
                    InstallDirectory: default,
                    //PkgTags: pkg.Tags,
                    //Source: pkg.Source,
                    InstalledVersion: pkg.InstalledVersion.DisplayName,
                    DefaultVersion: pkg.DefaultInstallVersion.DisplayName
                ));
            }
        }
        */

        // Get all installed items
        //var installedCatalogs = manager.GetLocalPackageCatalog;

        if (!gamesOnly)
        {
            //installed = GetInstalled();
            installed = GetInstalled(expandPackage);
            foreach (var item in installed)
            {
                if (item.Value.IsT0)
                {
                    yield return new ScoopGame(
                        Name: item.Key,
                        ShortcutName: "",
                        Prefix: default,
                        Binary: default,
                        IsInstalled: true,
                        //PkgTags: item.CatalogPackage.Tags,
                        //Source: item.CatalogPackage.Source,
                        InstallVersion: item.Value.AsT0.InstallVersion,
                        DefaultVersion: item.Value.AsT0.DefaultVersion,
                        Problems: ParseProblems(item.Value.AsT0.InstallInfo)
                    );
                }
                else yield return item.Value.AsT1;
            }
        }

        //var freeGames = SearchPackages();

        if (!installedOnly && !string.IsNullOrEmpty(query))
        {
            foreach (var game in SearchPackages(query, expandPackage))
            {
                if (!installed.ContainsKey(game.Key))
                {
                    if (game.Value.IsT0)
                    {
                        yield return new ScoopGame(
                            Name: game.Key,
                            ShortcutName: "",
                            Prefix: default,
                            Binary: default,
                            IsInstalled: false,
                            DefaultVersion: game.Value.AsT0.DefaultVersion
                        );
                    }
                    else yield return game.Value.AsT1;
                }
            }
        }
        if (!foundGameBucket)
        {
            process.StartInfo.Arguments = "bucket rm games";
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }
    }

    private Dictionary<ScoopGameId, OneOf<ScoopGame, ErrorMessage>> GetInstalled(bool expandPackage)
    {
        //*************************************
        Console.OutputEncoding = Encoding.UTF8;
        //*************************************

        Dictionary<ScoopGameId, OneOf<ScoopGame, ErrorMessage>> installed = [];

        var scoopCmd = FindClient();
        if (scoopCmd == default)
        {
            _logger?.LogDebug("***Scoop not installed");
            return new() { [ScoopGameId.From("")] = new ErrorMessage("Scoop not installed") };
        }


        //if (string.IsNullOrEmpty(output))
        //{
        //    _logger?.LogDebug("***No output from " + scoopCmd.GetFullPath() + process.StartInfo.Arguments);
        //    return new() { [ScoopGameId.From("")] = new ErrorMessage("No output from " + scoopCmd.GetFullPath() + process.StartInfo.Arguments) };
        //}

        List<int> colPos = [];
        var i = 0;
        /*
        foreach (var line in output.Split('\n'))
        {
            if (i == 0)
            {
                if (!line.Contains("Name ", StringComparison.Ordinal))
                    continue;

                var headLine = line[line.IndexOf("Name ", StringComparison.Ordinal)..];
                foreach (var col in headLine.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var c = headLine.IndexOf(col, StringComparison.Ordinal);
                    if (c > -1)
                        colPos.Add(c);
                }
                if (colPos.Count < 5)
                    break;
            }
            else if (i > 1 && !string.IsNullOrWhiteSpace(line)) // skip separator line
            {
                string name = "", version = "", available = "";
                string? source = null;
                // TODO: This doesn't work if there are surrogate pairs in the string
                if (line.Length > colPos[1])
                    name = line[colPos[0]..colPos[1]].Trim();
                if (line.Length > colPos[3])
                    version = line[colPos[2]..colPos[3]].Trim();
                if (line.Length > colPos[4])
                {
                    available = line[colPos[3]..colPos[4]].Trim();
                    source = line[colPos[4]..].Trim();
                }

                if (string.IsNullOrWhiteSpace(source)) // non-package install
                {
                    if (!expandPackage)
                    {
                        if (installed.TryAdd(id, new ScoopGame(
                            Name: name,
                            InstallDirectory: default,
                            IsInstalled: true,
                            InstalledVersion: version)))
                        {
                            _logger?.LogDebug("  " + name);
                        }
                        i++;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(name) && !name.EndsWith('…'))
                    {
                        var game = ParseRegistry(name);
                        if (game.IsT0)
                        {
                            if (installed.TryAdd(id, new ScoopGame(
                                Name: name,
                                InstallDirectory: game.AsT0.InstallDirectory,
                                Launch: game.AsT0.Launch,
                                Uninstall: game.AsT0.Uninstall,
                                IsInstalled: true,
                                InstallDate: game.AsT0.InstallDate,
                                Publisher: game.AsT0.Publisher,
                                SupportUrl: game.AsT0.SupportUrl,
                                Homepage: game.AsT0.Homepage,
                                InstalledVersion: version)))
                            {
                                _logger?.LogDebug("@ " + name);
                            }
                            i++;
                            continue;
                        }

                        if (installed.TryAdd(id, game.AsT1))
                        {
                            _logger?.LogDebug("***" + game.AsT1.Message);
                        }
                        i++;
                    }

                    if (installed.TryAdd(id, new ScoopGame(
                        Name: name,
                        InstallDirectory: default,
                        IsInstalled: true,
                        InstalledVersion: version)))
                    {
                        _logger?.LogDebug("? " + name);
                    }
                    i++;
                    continue;
                }

                //else [package install]

                if (!expandPackage)
                {
                    if (installed.TryAdd(id, new ScoopGame(
                        Name: name,
                        InstallDirectory: default,
                        IsInstalled: true,
                        Source: source,
                        InstalledVersion: version,
                        DefaultVersion: available)))
                    {
                        _logger?.LogDebug("  " + name);
                    }
                    i++;
                    continue;
                }

                if (installed.TryAdd(id, GetPackageInfo(colPos, line, search: false)))
                {
                    _logger?.LogDebug("  " + name);
                }
            }

            i++;
        }
        _logger?.LogDebug("GetInstalled(): " + i + " apps");
        */

        return installed;
    }

    private Dictionary<ScoopGameId, OneOf<ScoopGame, ErrorMessage>> SearchPackages(string query = DefaultQuery, bool expandPackage = false)
    {
        Dictionary<ScoopGameId, OneOf<ScoopGame, ErrorMessage>> freeGames = [];

        var scoopCmd = FindClient();

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = scoopCmd.GetFullPath();

        // Maybe use (much faster): https://github.com/shilangyu/scoop-search
        process.StartInfo.Arguments = $"search {query}";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // First ensure the "games" bucket has been added; though other user-defined buckets might be searched as well
        process.StartInfo.Arguments = "bucket add games";
        process.Start();
        output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // Enable partial searches on app names, binaries, and shortcuts
        process.StartInfo.Arguments = "config use_sqlite_cache true";
        process.Start();
        output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();


        if (string.IsNullOrEmpty(output))
        {
            _logger?.LogDebug("***No output from " + scoopCmd.GetFullPath() + process.StartInfo.Arguments);
            return new() { [ScoopGameId.From("")] = new ErrorMessage("No output from " + scoopCmd.GetFullPath() + process.StartInfo.Arguments) };
        }

        List<int> colPos = [];
        var i = 0;
        foreach (var line in output.Split('\n'))
        {
            if (i == 0)
            {
                if (!line.Contains("Name ", StringComparison.Ordinal))
                    continue;

                var headLine = line[line.IndexOf("Name ", StringComparison.Ordinal)..];
                foreach (var col in headLine.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var c = headLine.IndexOf(col, StringComparison.Ordinal);
                    if (c > -1)
                        colPos.Add(c);
                }
                if (colPos.Count < 5)
                    break;
            }
            else if (i > 1 && !string.IsNullOrWhiteSpace(line)) // skip separator line
            {
                string name = "", available = "", match = "";
                string? source = null;
                // TODO: This doesn't work if there are surrogate pairs in the string
                if (line.Length > colPos[1])
                    name = line[colPos[0]..colPos[1]].Trim();
                if (line.Length > colPos[3])
                    available = line[colPos[2]..colPos[3]].Trim();
                if (line.Length > colPos[4])
                {
                    match = line[colPos[3]..colPos[4]].Trim();
                    source = line[colPos[4]..].Trim();
                }

                var id = ScoopGameId.From(name);
                if (!expandPackage)
                {
                    if (freeGames.TryAdd(id, new ScoopGame(
                        Name: id,
                        ShortcutName: name,
                        Prefix: default,
                        Binary: default,
                        IsInstalled: false,
                        DefaultVersion: available)))
                    {
                        _logger?.LogDebug("* " + name);
                        i++;
                    }
                    continue;
                }

                if (freeGames.TryAdd(id, GetPackageInfo(name))) //GetPackageInfo(colPos, line, search: true)))
                {
                    _logger?.LogDebug("*@" + name);
                }
            }

            i++;
        }
        _logger?.LogDebug("SearchPackages(): " + i.ToString(CultureInfo.InvariantCulture) + " games");

        return freeGames;
    }

    [UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code",
    Justification = $"{nameof(JsonSerializerOptions)} uses {nameof(SourceGenerationContext)} for type information.")]
    private OneOf<ScoopGame, ErrorMessage> ParseManifest(string name)
    {
        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = "scoop.cmd";
        process.StartInfo.Arguments = $"cat {name}";
        process.Start();
        var catOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(catOutput))
            return new();

        var manifest = JsonSerializer.Deserialize<Manifest>(catOutput, JsonSerializerOptions);
        if (manifest is null)
            return new();

        /*
        return new ScoopGame(
            Name: name,
            ShortcutName: name, // TODO: ???
            Prefix: [],
            Binary: manifest.Bin?[0],
            Problems: ParseProblems(InstallInfo),
            Description: manifest.Description,
            DefaultVersion: manifest.Version,
            InstallVersion: manifest.Version, // TODO: ???
            Website: manifest.Homepage,
            LicenseType: manifest.License?.Identifier ?? manifest.License?.Tag,
            //Binary: manifest.Bin?[0],
            DownloadUrl: manifest.Url,
            Notes: string.Join(' ', manifest.Notes ?? [])
            //Notes: manifest.Notes is null ? "" : string.Join(' ', manifest.Notes)
        );
        */
        return new();
    }

    private OneOf<ScoopGame, ErrorMessage> GetPackageInfo(string name)
    {
        var scoopCmd = FindClient();
        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = scoopCmd.GetFullPath();
        //process.StartInfo.Arguments = $"-colors none -fields name show {name}";
        process.StartInfo.Arguments = $"info {name}";
        process.Start();
        var infoOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var description = "";
        var version = "";
        var bucket = "";
        var website = "";
        var license = "";
        var updatedAt = "";
        var updatedBy = "";
        var installed = "";
        var binaries = "";
        var notes = "";
        var prefix = "";

        foreach (var pkgLine in infoOutput.Split('\n'))
        {
            if (pkgLine.StartsWith("Description", StringComparison.Ordinal))
                description = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Version", StringComparison.Ordinal))
                version = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Bucket", StringComparison.Ordinal))
                bucket = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Website", StringComparison.Ordinal))
                website = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("License", StringComparison.Ordinal))
                license = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Updated at", StringComparison.Ordinal))
                updatedAt = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Updated by", StringComparison.Ordinal))
                updatedBy = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Installed", StringComparison.Ordinal))
                installed = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Binaries", StringComparison.Ordinal))
                binaries = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
            else if (pkgLine.StartsWith("Notes", StringComparison.Ordinal))
                notes = pkgLine[(pkgLine.IndexOf(':', StringComparison.Ordinal) + 2)..];
        }

        _logger?.LogDebug("> " + name);

        process.StartInfo.Arguments = $"prefix {name}";
        process.Start();
        var prefixOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        foreach (var pkgLine in infoOutput.Split('\n'))
        {
            if (Path.IsPathFullyQualified(pkgLine))
                prefix = pkgLine;
        }

        var id = ScoopGameId.From(name);
        return new ScoopGame(
            Name: id,
            ShortcutName: name,
            Prefix: default, //prefix,
            Binary: default, //binaries,
            Description: description,
            DefaultVersion: version,
            Bucket: bucket,
            Website: website,
            LicenseType: license,
            UpdateDate: default, //DateTime... updatedAt,
            //Updater: updatedBy,
            InstallVersion: installed,
            Notes: notes);
    }

    public IList<Problem> ParseProblems(string? InstallInfo)
    {
        List<Problem> p = [];

        if (InstallInfo is null)
            return [];

        if (InstallInfo.Contains("Install failed", StringComparison.Ordinal))
            p.Add(Problem.InstallFailed);
        if (InstallInfo.Contains("Manifest removed", StringComparison.Ordinal))
            p.Add(Problem.NotFoundInData);
        if (InstallInfo.Contains("missing", StringComparison.Ordinal))
            p.Add(Problem.NotFoundOnDisk);
        if (InstallInfo.Contains("Held package", StringComparison.Ordinal))
            p.Add(Problem.VersionLocked);

        return p;
    }
}
