using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
//using System.Security.Principal;
using System.Text;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.PkgHandlers.Winget;

/// <summary>
/// Handler for finding installed apps via Windows Package Manager.
/// </summary>
/// <remarks>
/// Constructor. Leverages winget, which inspects registry and WindowsApps (Microsoft Store) for installed programs.
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
public class WingetHandler(IRegistry registry, IFileSystem fileSystem, ILogger? logger = null) : AHandler<WingetGame, WingetGameId>
{
    internal const string DefaultQuery = "game";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger? _logger = logger ?? NullLogger<WingetHandler>.Instance;

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
    public override IEqualityComparer<WingetGameId>? IdEqualityComparer => WingetGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<WingetGame, WingetGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetExe = _fileSystem.FromUnsanitizedFullPath(Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe"));
        if (Path.IsPathFullyQualified(localAppData) && wingetExe.FileExists)
            return wingetExe;

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<WingetGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        return FindAllGames(settings?.InstalledOnly ?? false, settings?.OwnedOnly ?? false, settings?.GamesOnly ?? false, settings?.StoreOnly ?? false);
    }

    /// <summary>
    /// Finds all apps supported by this package handler. The return type
    /// will always be a non-null game or a non-null error message.
    /// </summary>
    /// <param name="installedOnly"></param>
    /// <param name="ownedOnly"></param>
    /// <param name="gamesOnly"></param>
    /// <param name="storeOnly"></param>
    /// <param name="query"></param>
    /// <param name="expandPackage">Get additional information about winget/msstore packages</param>
    /// <returns></returns>
    public IEnumerable<OneOf<WingetGame, ErrorMessage>> FindAllGames(
        bool installedOnly = false,
        bool ownedOnly = false,
        bool gamesOnly = false,
        bool storeOnly = false,
        string? query = DefaultQuery,
        bool expandPackage = false)
    {
        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = [];

        if (!ownedOnly)
            installed = GetInstalled(expandPackage);

        /*
        foreach (var catalog in availableCatalogs.ToArray())
        {
            foreach (var game in SearchPackages(query, expandPackage))
            {
                var pkg = match.CatalogPackage;
                var id = WingetGameId.From(pkg.Id);

                installed.TryAdd(id, new WingetGame(
                    Id: id,
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
                    yield return new WingetGame(
                        Id: item.Key,
                        Name: item.Value.AsT0.Name,
                        InstallDirectory: default,
                        IsInstalled: true,
                        //PkgTags: item.CatalogPackage.Tags,
                        //Source: item.CatalogPackage.Source,
                        InstalledVersion: item.Value.AsT0.InstalledVersion,
                        DefaultVersion: item.Value.AsT0.DefaultVersion
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
                        yield return new WingetGame(
                            Id: game.Key,
                            Name: game.Value.AsT0.Name,
                            InstallDirectory: default,
                            IsInstalled: false,
                            DefaultVersion: game.Value.AsT0.DefaultVersion
                        );
                    }
                    else yield return game.Value.AsT1;
                }
            }
        }
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> GetInstalled(bool expandPackage)
    {
        //*************************************
        Console.OutputEncoding = Encoding.UTF8;
        //*************************************

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = [];

        var wingetExe = FindClient();
        if (wingetExe == default)
        {
            _logger?.LogDebug("***Winget not installed");
            return new() { [WingetGameId.From("")] = new ErrorMessage("Winget not installed") };
        }

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = wingetExe.GetFullPath();
        process.StartInfo.Arguments = "list --nowarn --disable-interactivity";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            _logger?.LogDebug("***No output from " + wingetExe.GetFullPath() + process.StartInfo.Arguments);
            return new() { [WingetGameId.From("")] = new ErrorMessage("No output from " + wingetExe.GetFullPath() + process.StartInfo.Arguments) };
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
                string name = "", strId = "", version = "", available = "";
                string? source = null;
                WingetGameId id = default;
                // TODO: This doesn't work if there are surrogate pairs in the string
                if (line.Length > colPos[1])
                    name = line[colPos[0]..colPos[1]].Trim();
                if (line.Length > colPos[2])
                {
                    strId = line[colPos[1]..colPos[2]].Trim();
                    id = WingetGameId.From(strId);
                }
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
                        if (installed.TryAdd(id, new WingetGame(
                            Id: id,
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

                    if (name.EndsWith('…') || strId.EndsWith('…') || version.EndsWith('…')) // ...
                    {
                        (name, strId, version) = Relist(name, strId, version, source);
                        strId = strId.TrimEnd('…');
                        id = WingetGameId.From(strId);
                    }

                    if (!string.IsNullOrEmpty(strId) && !strId.EndsWith('…'))
                    {
                        var game = ParseRegistry(strId);
                        if (game.IsT0)
                        {
                            if (installed.TryAdd(id, new WingetGame(
                                Id: id,
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

                    if (installed.TryAdd(id, new WingetGame(
                        Id: id,
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
                    if (name.EndsWith('…') || strId.EndsWith('…') || version.EndsWith('…')) // ...
                    {
                        (name, strId, version) = Relist(name, strId, version, source);
                        strId = strId.TrimEnd('…');
                        id = WingetGameId.From(strId);
                    }

                    if (installed.TryAdd(id, new WingetGame(
                        Id: id,
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

                if (strId.EndsWith('…')) // ...
                {
                    (name, strId, version) = Relist(name, strId, version, source);
                    strId = strId.TrimEnd('…');
                    id = WingetGameId.From(strId);
                }
                if (installed.TryAdd(id, GetPackageInfo(colPos, line, search: false)))
                {
                    _logger?.LogDebug("  " + name);
                }
            }

            i++;
        }
        _logger?.LogDebug("GetInstalled(): " + i.ToString(CultureInfo.InvariantCulture) + " apps");

        return installed;
    }

    private (string, string, string) Relist(string name, string id, string version, string? source)
    {
        var wingetExe = FindClient();

        if (string.IsNullOrWhiteSpace(source))
            source = "winget";

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = wingetExe.GetFullPath();
        //process.StartInfo.Arguments = $"list --name \"{name.TrimEnd('…')}\" --source {source} --nowarn --disable-interactivity";
        process.StartInfo.Arguments = $"list --id \"{id.TrimEnd('…')}\" --source {source} --nowarn --disable-interactivity";
        process.Start();
        var listOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        List<int> listCols = [];
        var i = 0;
        foreach (var listLine in listOutput.Split('\n'))
        {
            if (i == 0)
            {
                if (!listLine.Contains("Name", StringComparison.Ordinal))
                    continue;

                var headLine = listLine[listLine.IndexOf("Name ", StringComparison.Ordinal)..];
                foreach (var col in headLine.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var c = headLine.IndexOf(col, StringComparison.Ordinal);
                    if (c > -1)
                        listCols.Add(c);
                }
                if (listCols.Count < 3)
                    break;
            }
            else if (i > 1 && !string.IsNullOrWhiteSpace(listLine))  // skip separator line
            {
                // TODO: This doesn't work if there are surrogate pairs in the string
                if (listLine.Length > listCols[1])
                    name = listLine[listCols[0]..listCols[1]].Trim();
                if (listLine.Length > listCols[2])
                {
                    id = listLine[listCols[1]..listCols[2]].Trim();
                    version = listLine[listCols[2]..].Trim();
                }
            }

            i++;
        }

        return (name, id, version);
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> SearchPackages(string query = DefaultQuery, bool expandPackage = false)
    {
        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> freeGames = [];

        var wingetExe = FindClient();

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = wingetExe.GetFullPath();
        process.StartInfo.Arguments = $"search --tag {query} --source winget --nowarn --disable-interactivity";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            _logger?.LogDebug("***No output from " + wingetExe.GetFullPath() + process.StartInfo.Arguments);
            return new() { [WingetGameId.From("")] = new ErrorMessage("No output from " + wingetExe.GetFullPath() + process.StartInfo.Arguments) };
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
                string name = "", strId = "", available = "", match = "";
                string? source = null;
                WingetGameId id = default;
                // TODO: This doesn't work if there are surrogate pairs in the string
                if (line.Length > colPos[1])
                    name = line[colPos[0]..colPos[1]].Trim();
                if (line.Length > colPos[2])
                {
                    strId = line[colPos[1]..colPos[2]].Trim();
                    id = WingetGameId.From(strId);
                }
                if (line.Length > colPos[3])
                    available = line[colPos[2]..colPos[3]].Trim();
                if (line.Length > colPos[4])
                {
                    match = line[colPos[3]..colPos[4]].Trim();
                    source = line[colPos[4]..].Trim();
                }

                if (!expandPackage)
                {
                    if (freeGames.TryAdd(id, new WingetGame(
                        Id: id,
                        Name: name,
                        InstallDirectory: default,
                        IsInstalled: false,
                        DefaultVersion: available)))
                    {
                        _logger?.LogDebug("* " + name);
                        i++;
                    }
                    continue;
                }

                if (freeGames.TryAdd(id, GetPackageInfo(colPos, line, search: true)))
                {
                    _logger?.LogDebug("*@" + name);
                }
            }

            i++;
        }
        _logger?.LogDebug("SearchPackages(): " + i.ToString(CultureInfo.InvariantCulture) + " games");

        return freeGames;
    }

    private OneOf<WingetGame, ErrorMessage> ParseRegistry(string id)
    {
        // id syntax = "ARP\[Machine|User]\[X64|X86]\

        string? regKeyName; // = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
        IRegistryKey baseKey; // = _registry.OpenBaseKey(RegistryHive.LocalMachine);
        if (id.StartsWith(@"ARP\Machine\X64\", StringComparison.Ordinal))
        {
            regKeyName = UninstallRegKey + id[@"ARP\Machine\X64\".Length..];
            baseKey = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }
        else if (id.StartsWith(@"ARP\Machine\X86\", StringComparison.Ordinal))
        {
            regKeyName = UninstallRegKey + id[@"ARP\Machine\X86\".Length..];
            baseKey = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        }
        else if (id.StartsWith(@"ARP\User\X64\", StringComparison.Ordinal))
        {
            regKeyName = UninstallRegKey + id[@"ARP\User\X64\".Length..];
            baseKey = _registry.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        }
        else if (id.StartsWith(@"ARP\User\X86\", StringComparison.Ordinal))
        {
            regKeyName = UninstallRegKey + id[@"ARP\User\X86\".Length..];
            baseKey = _registry.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
        }
        else
            return new ErrorMessage("Did not find expected \"ARP\\[Machine|User]\\[X64|X86]\\\" registry key prefix in id " + id);

        try
        {
            using var regKey = baseKey.OpenSubKey(regKeyName);
            if (regKey is null)
            {
                return new ErrorMessage($"Unable to open {regKeyName}");
            }

            if (!regKey.TryGetString("DisplayName", out var name))
                name = "";

            if (!regKey.TryGetString("HelpLink", out var help))
                help = "";

            DateTime installDate = default;
            if (regKey.TryGetString("InstallDate", out var date))
                DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out installDate);

            if (!regKey.TryGetString("InstallLocation", out var path))
                path = "";

            if (!regKey.TryGetString("DisplayIcon", out var launch))
                launch = "";

            if (!regKey.TryGetString("Publisher", out var pub))
                pub = "";

            if (!regKey.TryGetString("UninstallString", out var uninst))
                uninst = "";

            if (!regKey.TryGetString("URLInfoAbout", out var url))
                url = "";

            return new WingetGame(
                Id: WingetGameId.From(id),
                Name: name,
                InstallDirectory: Path.IsPathFullyQualified(path) ? _fileSystem.FromUnsanitizedFullPath(path) : new(),
                InstallDate: installDate == default ? null : installDate,
                Launch: Path.IsPathFullyQualified(launch) ? _fileSystem.FromUnsanitizedFullPath(launch) : new(),
                Uninstall: Path.IsPathFullyQualified(uninst) ? _fileSystem.FromUnsanitizedFullPath(uninst) : new(),
                Publisher: pub,
                SupportUrl: help,
                Homepage: url
            );
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {regKeyName}");
        }
    }

    private OneOf<WingetGame, ErrorMessage> GetPackageInfo(List<int> colPos, string listInfo, bool search = false)
    {
        //list header = "Name,Id,Version,Available,Source"
        //search header = "Name,Id,Version,Match,Source"

        var wingetExe = FindClient();

        var isDescription = false;
        //var isReleaseNotes = false;
        var isTags = false;

        var name = listInfo[colPos[0]..colPos[1]].Trim();
        var strId = listInfo[colPos[1]..colPos[2]].Trim();
        var id = WingetGameId.From(strId);
        var version = "";
        string? available;
        //string? match;
        if (search)
        {
            available = listInfo[colPos[2]..colPos[3]].Trim();
            //match = listInfo[colPos[3]..colPos[4]].Trim();
        }
        else
        {
            version = listInfo[colPos[2]..colPos[3]].Trim();
            available = listInfo[colPos[3]..colPos[4]].Trim();
        }
        var source = listInfo[colPos[4]..].Trim();

        if (strId.EndsWith('…')) // ...
        {
            (name, strId, version) = Relist(name, strId, version, source);
            id = WingetGameId.From(strId);
        }

        var pkgName = "";
        var publisher = "";
        //var publisherUrl = "";
        var publisherSupportUrl = "";
        var author = "";
        var moniker = "";
        var description = "";
        var homepage = "";
        var license = "";
        //var licenseUrl = "";
        //var privacyUrl = "";
        //var copyright = "";
        //var releaseNotes = "";
        //var releaseNotesUrl = "";
        //var purchaseUrl = "";
        //var documentationTutorials = "";
        //var documentationManual = "";
        List<string> tags = [];
        //var agreementsCategory = "";
        //var agreementsPricing = "";
        //string agreementsFreeTrial = "";
        var agreementsAgeRatings = "";
        //string agreementsTermsOfTransaction = "";
        //string agreementsSeizureWarning = "";
        //string agreementsStoreLicenseTerms = "";
        //var installerType = "";
        //var installerLocale = "";
        var installerUrl = "";
        //var installerSha256 = "";
        //var installerStoreProductId = "";
        //var installerReleaseDate = "";
        //var installerOfflineDistributionSupported = false;

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = wingetExe.GetFullPath();
        process.StartInfo.Arguments = $"show {strId} --nowarn --disable-interactivity";
        process.Start();
        var pkgOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var i = 0;
        foreach (var pkgLine in pkgOutput.Split('\n'))
        {
            if (isDescription)
            {
                if (pkgLine.StartsWith(' '))
                {
                    description += pkgLine.TrimStart();
                    continue;
                }
                isDescription = false;
            }
            /*
            if (isReleaseNotes)
            {
                if (pkgLine.StartsWith(' '))
                {
                    releaseNotes += pkgLine.TrimStart();
                    continue;
                }
                isReleaseNotes = false;
            }
            */
            if (isTags)
            {
                if (pkgLine.StartsWith(' '))
                {
                    tags.Add(pkgLine.TrimStart());
                    continue;
                }
                isTags = false;
            }

            if (i == 0)
            {
                if (pkgLine.Contains("Found ", StringComparison.Ordinal))
                {
                    var headLine = pkgLine[pkgLine.IndexOf("Found ", StringComparison.Ordinal)..];
                    if (headLine.LastIndexOf('[') > 7)
                    {
                        strId = headLine[(headLine.LastIndexOf('[') + 1)..^1];
                        id = WingetGameId.From(strId);
                        pkgName = headLine["Found ".Length..headLine.LastIndexOf('[')];
                    }
                    else
                        pkgName = headLine["Found ".Length..];
                }
                else
                    continue;
            }
            else if (pkgLine.StartsWith("Version: ", StringComparison.Ordinal))
                version = pkgLine["Version: ".Length..];
            else if (pkgLine.StartsWith("Publisher: ", StringComparison.Ordinal))
                publisher = pkgLine["Publisher: ".Length..];
            else if (pkgLine.StartsWith("Publisher Support Url: ", StringComparison.Ordinal))
                publisherSupportUrl = pkgLine["Publisher Support Url: ".Length..];
            else if (pkgLine.StartsWith("Moniker: ", StringComparison.Ordinal))
                moniker = pkgLine["Moniker: ".Length..];
            else if (pkgLine.StartsWith("Description:", StringComparison.Ordinal))
            {
                description = pkgLine["Description:".Length..].TrimStart();
                isDescription = true;
            }
            else if (pkgLine.StartsWith("Homepage: ", StringComparison.Ordinal))
                homepage = pkgLine["Homepage: ".Length..];
            else if (pkgLine.StartsWith("License: ", StringComparison.Ordinal))
                license = pkgLine["License: ".Length..];
            //else if (pkgLine.StartsWith("Release Notes:", StringComparison.Ordinal))
            //{
            //    releaseNotes = pkgLine["Release Notes:".Length..].TrimStart();
            //    isReleaseNotes = true;
            //}
            else if (pkgLine.StartsWith("  Age Ratings: ", StringComparison.Ordinal))
                agreementsAgeRatings = pkgLine["  Age Ratings: ".Length..];
            else if (pkgLine.StartsWith("Tags:", StringComparison.Ordinal))
                isTags = true;
            else if (pkgLine.StartsWith("  Installer Url: ", StringComparison.Ordinal))
                installerUrl = pkgLine["  Installer Url: ".Length..];

            i++;
        }

        _logger?.LogDebug("> " + name);
        return new WingetGame(
            Id: id,
            Name: name,
            InstallDirectory: default,
            InstallerUrl: installerUrl,
            IsInstalled: true,
            Description: description,
            Publisher: publisher,
            Author: author,
            PackageTags: tags,
            Homepage: homepage,
            SupportUrl: publisherSupportUrl,
            PackageName: pkgName,
            Moniker: moniker,
            Source: source,
            LicenseType: license,
            InstalledVersion: version,
            DefaultVersion: available,
            AgeRating: agreementsAgeRatings);
    }
}
