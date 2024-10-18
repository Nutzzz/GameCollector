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
using NexusMods.Paths;
using OneOf;

namespace GameCollector.PkgHandlers.Winget;

/// <summary>
/// Handler for finding installed apps via Windows Package Manager.
/// </summary>
/// <remarks>
/// Leverages winget, which inspects registry and WindowsApps (Microsoft Store) for installed programs.
/// </remarks>
[PublicAPI]
public class WingetHandler : AHandler<WingetGame, WingetGameId>
{
    internal const string DefaultQuery = "game";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;
    private ILogger? _logger;

    //private WindowsPackageManagerFactory? _winGetFactory = null;

    private ProcessStartInfo _startInfo = new()
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true,
    };

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <c>null</c>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface.
    /// </param>
    public WingetHandler(IRegistry registry, IFileSystem fileSystem, ILogger? logger = null)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _logger = logger;

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0))
        {
            // If the user is an administrator, use the elevated factory. Otherwhise COM will crash
            /*
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                _winGetFactory = new WindowsPackageManagerElevatedFactory();
            else
                _winGetFactory = new WindowsPackageManagerStandardFactory();
            */
        }
    }

    /// <inheritdoc/>
    public override IEqualityComparer<WingetGameId>? IdEqualityComparer => WingetGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<WingetGame, WingetGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetExe = _fileSystem.FromUnsanitizedFullPath(Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe"));
        if (Path.IsPathRooted(localAppData) && _fileSystem.FileExists(wingetExe))
            return wingetExe;

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<WingetGame, ErrorMessage>> FindAllGames(Settings? settings = null)
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
    /// <param name="expandPackage">Get additional information about winget/msstore packages</param>
    /// <returns></returns>
    public IEnumerable<OneOf<WingetGame, ErrorMessage>> FindAllGames(
        bool installedOnly = false,
        bool ownedOnly = false,
        bool gamesOnly = false,
        string? query = DefaultQuery,
        bool expandPackage = false)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0))
        {
            yield return new ErrorMessage("Only supported on Windows 10.0.19041.0 or later.");
            yield break;
        }
        /*
        if (_winGetFactory is null)
        {
            yield return new ErrorMessage("Could not access Windows Package Manager.");
            yield break;
        }
        */

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = new();

        // Create Package Manager and get available catalogs
        //var manager = _winGetFactory.CreatePackageManager();
        //var availableCatalogs = manager.GetPackageCatalogs();

        /*
        foreach (var catalog in availableCatalogs.ToArray())
        {
            //var filterList = _winGetFactory.CreateFindPackagesOptions();
            // Create a filter to search for packages with query ["game" default]

            // Add the query to the filter
            var tagFilter = _winGetFactory.CreatePackageMatchFilter();
            tagFilter.Field = PackageMatchField.Tag;
            tagFilter.Value = query;
            filterList.Filters.Add(tagFilter);

            // Find the packages with the filters
            //var searchResults = await catalog.Connect().PackageCatalog.FindPackagesAsync(filterList);
            var searchResults = catalog.Connect().PackageCatalog.FindPackages(filterList);
            foreach (var match in searchResults.Matches.ToArray())
            {
                var pkg = match.CatalogPackage;
                var id = WingetGameId.From(pkg.Id);

                installed.Add(id, new WingetGame(
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
            installed = GetInstalledParse(expandPackage);
            foreach (var item in installed)
            {
                if (item.Value.IsT0)
                {
                    yield return new WingetGame(
                        Id: item.Key,
                        Name: item.Value.AsT0.Name,
                        InstallDirectory: default,
                        IsInstalled: true,
                        IsOwned: true,
                        //PkgTags: item.CatalogPackage.Tags,
                        //Source: item.CatalogPackage.Source,
                        InstalledVersion: item.Value.AsT0.InstalledVersion,
                        DefaultVersion: item.Value.AsT0.DefaultVersion
                    );
                }
                else yield return item.Value.AsT1;
            }
        }

        //var freeGames = SearchFreeGames();

        if (!installedOnly && !string.IsNullOrEmpty(query))
        {
            foreach (var game in SearchFreeGamesParse(query, expandPackage))
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
                            IsOwned: false,
                            DefaultVersion: game.Value.AsT0.DefaultVersion
                        );
                    }
                    else yield return game.Value.AsT1;
                }
            }
        }
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> GetInstalled()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0))
        {
            _logger?.LogDebug("***Only supported on Windows 10.0.19041.0 or later.");
            return new() { [WingetGameId.From("")] = new ErrorMessage("Only supported on Windows 10.0.19041.0 or later.") };
        }
        /*
        if (_winGetFactory is null)
        {
            _logger?.LogDebug("***Could not access Windows Package Manager.");
            return new() { [WingetGameId.From("")] = new ErrorMessage("Could not access Windows Package Manager.") };
        }
        */

        // Fetching installed packages
        //var winGetManager = _winGetFactory.CreatePackageManager();

        // CHANGE THIS INDEX TO CHANGE THE SOURCE
        var selectedIndex = 0; // does this refer to winget vs. msstore?

        /*
        PackageCatalogReference installedSearchCatalogRef;
        if (selectedIndex < 0)
        {
            installedSearchCatalogRef = winGetManager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
        }
        else
        {
            var selectedRemoteCatalogRef = winGetManager.GetPackageCatalogs().ToArray()[selectedIndex];

            //_logger?.LogDebug($"Searching on package catalog {selectedRemoteCatalogRef.Info.Name} ");

            var createCompositePackageCatalogOptions = _winGetFactory.CreateCreateCompositePackageCatalogOptions();
            createCompositePackageCatalogOptions.Catalogs.Add(selectedRemoteCatalogRef);
            createCompositePackageCatalogOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;
            installedSearchCatalogRef = winGetManager.CreateCompositePackageCatalog(createCompositePackageCatalogOptions);
        }

        var connectResult = installedSearchCatalogRef.Connect();
        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            yield return new ErrorMessage("Failed to connect to local catalog.");
            yield break;
        }

        var findPackagesOptions = _winGetFactory.CreateFindPackagesOptions();
        var filter = _winGetFactory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
        filter.Value = "";
        findPackagesOptions.Filters.Add(filter);

        var taskResult = connectResult.PackageCatalog.FindPackages(findPackagesOptions);
        */

        // Begin enumeration
        /*
        foreach (var match in taskResult.Matches.ToArray())
        {
            var pkg = match.CatalogPackage;
            //_logger?.LogDebug($"Package {pkg.Name} is available Online: " + pkg.DefaultInstallVersion.PackageCatalog.Info.Name);
            yield return new WingetGame(
                Id: WingetGameId.From(pkg.Id),
                Name: pkg.Name,
                InstallDirectory: default,
                IsInstalled: pkg.DefaultInstallVersion != null,
                IsOwned: pkg.DefaultInstallVersion != null,
                //PackageTags: pkg.Tags???,
                Source: pkg.DefaultInstallVersion?.Channel, // is this winget vs. msstore?
                PackageName: pkg.DefaultInstallVersion?.PackageCatalog.Info.Name,
                //ProductCodes: pkg.DefaultInstallVersion?.ProductCodes.ToList(),
                Publisher: pkg.InstalledVersion.Publisher,
                InstalledVersion: pkg.InstalledVersion.DisplayName,
                DefaultVersion: pkg.DefaultInstallVersion?.DisplayName);
        }
        */

        // End enumeration
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> GetInstalled(bool? expandPackage)
    {
        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = new();
        return installed;
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> GetInstalledParse(bool expandPackage)
    {
        //*************************************
        Console.OutputEncoding = Encoding.UTF8;
        //*************************************

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = new();

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
            _logger?.LogDebug("***No output from winget");
            return new() { [WingetGameId.From("")] = new ErrorMessage("No output from winget") };
        }

        List<int> colPos = new();
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
                            IsOwned: true,
                            InstalledVersion: version)))
                        {
                            _logger?.LogDebug("  " + name);
                        }
                        i++;
                        continue;
                    }

                    if (name.EndsWith('…') || strId.EndsWith('…') || version.EndsWith('…'))
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
                                IsOwned: true,
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
                        IsOwned: true,
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
                    if (name.EndsWith('…') || strId.EndsWith('…') || version.EndsWith('…'))
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
                        IsOwned: true,
                        Source: source,
                        InstalledVersion: version,
                        DefaultVersion: available)))
                    {
                        _logger?.LogDebug("  " + name);
                    }
                    i++;
                    continue;
                }

                if (strId.EndsWith('…'))
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
        _logger?.LogDebug("GetInstalledParse(): " + i + " apps");

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

        List<int> listCols = new();
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

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> SearchFreeGamesParse(string query = DefaultQuery, bool expandPackage = false)
    {
        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> freeGames = new();

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
            _logger?.LogDebug("***No output from winget");
            return new() { [WingetGameId.From("")] = new ErrorMessage("No output from winget") };
        }

        List<int> colPos = new();
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
                        IsOwned: false,
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
        _logger?.LogDebug("SearchFreeGamesParse(): " + i + " games");

        return freeGames;
    }

    private OneOf<WingetGame, ErrorMessage> ParseRegistry(string id)
    {
        // id syntax = "ARP\[Machine|User]\[X64|X86]\

        var regKeyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
        var baseKey = _registry.OpenBaseKey(RegistryHive.LocalMachine);
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
                DateTime.TryParseExact(date, "yyyyMMdd", new CultureInfo("en-US"), DateTimeStyles.None, out installDate);

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
                InstallDirectory: Path.IsPathRooted(path) ? _fileSystem.FromUnsanitizedFullPath(path) : new(),
                InstallDate: installDate == default ? null : installDate,
                Launch: Path.IsPathRooted(launch) ? _fileSystem.FromUnsanitizedFullPath(launch) : new(),
                Uninstall: Path.IsPathRooted(uninst) ? _fileSystem.FromUnsanitizedFullPath(uninst) : new(),
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
        List<string> tags = new();
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
            IsOwned: true,
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
