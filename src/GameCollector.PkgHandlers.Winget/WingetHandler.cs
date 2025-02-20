using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Management.Deployment;
using NexusMods.Paths;
using OneOf;
using GameCollector.PkgHandlers.Winget.WindowsPackageManager;

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

    private WindowsPackageManagerFactory? _factory = null;
    private static WindowsPackageManagerFactory? _externalFactory;
    private PackageManager? _wingetManager;
    private static PackageManager? _externalWingetManager;
    private ProcessStartInfo _startInfo = new()
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
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
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                _factory = new WindowsPackageManagerElevatedFactory();
                //_factory = new WindowsPackageManagerStandardFactory(allowLowerTrustRegistration: true);
            else
                _factory = new WindowsPackageManagerStandardFactory();

            // Create Package Manager and get available catalogs
            _wingetManager = _factory?.CreatePackageManager();
            _externalFactory = _factory;
            _externalWingetManager = _wingetManager;
        }
    }

    /// <inheritdoc/>
    public override IEqualityComparer<WingetGameId>? IdEqualityComparer => WingetGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<WingetGame, WingetGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        // Not necessary if using API
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
        //_logger?.LogDebug("***FindAllGames()");
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0))
        {
            //_logger?.LogDebug("***Only supported on Windows 10.0.19041.0 or later.");
            yield return new ErrorMessage("Only supported on Windows 10.0.19041.0 or later.");
            yield break;
        }
        if (_wingetManager is null)
        {
            //_logger?.LogDebug("***Could not access Windows Package Manager.");
            yield return new ErrorMessage("Could not access Windows Package Manager.");
            yield break;
        }

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> items = new();

        if (!ownedOnly)
            items = GetInstalled();

        if (!installedOnly && !string.IsNullOrEmpty(query))
        {
            foreach (var game in SearchFreeGames(query, expandPackage))
            {
                items.TryAdd(game.Key, game.Value);
            }
        }

        foreach (var item in items)
        {
            //_logger?.LogDebug("***" + item.Value.ToString());
            yield return item.Value;
        }
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> GetInstalled()
    {
        //_logger?.LogDebug("***GetInstalled()");
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0))
        {
            return new() { [WingetGameId.From("")] = new ErrorMessage("Only supported on Windows 10.0.19041.0 or later.") };
        }
        if (_wingetManager is null || _factory is null)
        {
            return new() { [WingetGameId.From("")] = new ErrorMessage("Could not access Windows Package Manager.") };
        }

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = new();

        // CHANGE THIS INDEX TO CHANGE THE SOURCE
        var selectedIndex = 0; // TODO: I suspect this refers to winget vs. msstore

        PackageCatalogReference installedSearchCatalogRef;
        if (selectedIndex < 0)
        {
            installedSearchCatalogRef = _wingetManager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
        }
        else
        {
            var selectedRemoteCatalogRef = _wingetManager.GetPackageCatalogs().ToArray()[selectedIndex];

            //_logger?.LogDebug($"***Searching on package catalog {selectedRemoteCatalogRef.Info.Name} ");

            var createCompositePackageCatalogOptions = _factory.CreateCreateCompositePackageCatalogOptions();
            createCompositePackageCatalogOptions.Catalogs.Add(selectedRemoteCatalogRef);
            createCompositePackageCatalogOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;
            installedSearchCatalogRef = _wingetManager.CreateCompositePackageCatalog(createCompositePackageCatalogOptions);
        }

        var connectResult = installedSearchCatalogRef.Connect();
        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            //_logger?.LogDebug("***Failed to connect to local catalog.");
            return new() { [WingetGameId.From("")] = new ErrorMessage("Failed to connect to local catalog.") };
        }

        var findPackagesOptions = _factory.CreateFindPackagesOptions();
        var filter = _factory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
        filter.Value = "";
        findPackagesOptions.Filters.Add(filter);

        var taskResult = connectResult.PackageCatalog.FindPackages(findPackagesOptions);

        // Begin enumeration
        foreach (var match in taskResult.Matches.ToArray())
        {
            var pkg = match.CatalogPackage;
            if (pkg is null)
                continue;

            installed.TryAdd(WingetGameId.From(pkg.Id), new WingetGame(
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
                DefaultVersion: pkg.DefaultInstallVersion?.DisplayName));
        }
        return installed;
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> GetInstalledParse(bool expandPackage)
    {
        //_logger?.LogDebug("***GetInstalledParse()");
#if DEBUG
        Console.OutputEncoding = Encoding.UTF8;
#endif

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> installed = new();

        var wingetExe = FindClient();
        if (wingetExe == default)
        {
            //_logger?.LogDebug("***Winget not installed");
            return new() { [WingetGameId.From("")] = new ErrorMessage("Winget not installed") };
        }
#if DEBUG
        Console.WriteLine("wingetExe: " + wingetExe.GetFullPath());
#endif

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = wingetExe.GetFullPath();
        process.StartInfo.Arguments = "list --nowarn --disable-interactivity";
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            //_logger?.LogDebug("***No output from winget");
            return new() { [WingetGameId.From("")] = new ErrorMessage("No output from winget") };
        }
#if DEBUG
        Console.WriteLine("[" + output + "]");
#endif

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
                            //_logger?.LogDebug("  " + name);
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

                    if (!string.IsNullOrEmpty(strId) &&
                        strId.StartsWith(@"ARP\", StringComparison.Ordinal) &&
                        !strId.EndsWith('…'))
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
                                //_logger?.LogDebug("@ " + name);
                            }
                            i++;
                            continue;
                        }

                        if (installed.TryAdd(id, game.AsT1))
                        {
                            //_logger?.LogDebug("***" + game.AsT1.Message);
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
                        //_logger?.LogDebug("? " + name);
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
                        //_logger?.LogDebug("  " + name);
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
                    //_logger?.LogDebug("  " + name);
                }
            }

            i++;
        }
        //_logger?.LogDebug("GetInstalledParse(): " + i + " apps");

        return installed;
    }

    private (string, string, string) Relist(string name, string id, string version, string? source = null)
    {
        //_logger?.LogDebug("***Relist()");
        if (string.IsNullOrWhiteSpace(source))
            source = "winget";

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = FindClient().GetFullPath();
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
                // TODO: This doesn't work if there are UTF-16 surrogate pairs in the string
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

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> SearchFreeGames(string query = DefaultQuery, bool expandPackage = false)
    {
        //_logger?.LogDebug("***SearchFreeGames()");
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0))
        {
            return new() { [WingetGameId.From("")] = new ErrorMessage("Only supported on Windows 10.0.19041.0 or later.") };
        }
        if (_wingetManager is null || _factory is null)
        {
            return new() { [WingetGameId.From("")] = new ErrorMessage("Could not access Windows Package Manager.") };
        }

        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> freeGames = new();
        var availableCatalogs = _wingetManager.GetPackageCatalogs();

        foreach (var catalog in availableCatalogs.ToArray())
        {
            // Create a filter to search for packages with query ["game" default]
            var filterList = _factory.CreateFindPackagesOptions();

            // Add the query to the filter
            var tagFilter = _factory.CreatePackageMatchFilter();
            tagFilter.Field = PackageMatchField.Tag;
            tagFilter.Value = query;
            filterList.Filters.Add(tagFilter);

            // Find the packages with the filters
            //var searchResults = await catalog.ConnectAsync().GetResults().PackageCatalog.FindPackagesAsync(filterList);
            var searchResults = catalog.Connect().PackageCatalog.FindPackages(filterList);
            foreach (var match in searchResults.Matches.ToArray())
            {
                var pkg = match.CatalogPackage;
                var id = WingetGameId.From(pkg.Id);

                freeGames.Add(id, new WingetGame(
                    Id: id,
                    Name: pkg.Name,
                    InstallDirectory: default,
                    PackageTags: pkg.DefaultInstallVersion.PackageFamilyNames.ToList(),
                    Publisher: pkg.DefaultInstallVersion.Publisher,
                    Source: pkg.DefaultInstallVersion.Channel,
                    DefaultVersion: pkg.DefaultInstallVersion.DisplayName
                ));

                //_logger?.LogDebug("* " + pkg.Name);
            }
        }

        return new();
    }

    private Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> SearchFreeGamesParse(string query = DefaultQuery, bool expandPackage = false)
    {
        //_logger?.LogDebug("***SearchFreeGamesParse()");
        Dictionary<WingetGameId, OneOf<WingetGame, ErrorMessage>> freeGames = new();

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = FindClient().GetFullPath();
        process.StartInfo.Arguments = $"search --tag {query} --source winget --nowarn --disable-interactivity";
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            //_logger?.LogDebug("***No output from winget");
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
                // TODO: This doesn't work if there are UTF-16 surrogate pairs in the string
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
                        //_logger?.LogDebug("* " + name);
                        i++;
                    }
                    continue;
                }

                if (freeGames.TryAdd(id, GetPackageInfo(colPos, line, search: true)))
                {
                    //_logger?.LogDebug("*@" + name);
                }
            }

            i++;
        }
        //_logger?.LogDebug("SearchFreeGamesParse(): " + i + " games");

        return freeGames;
    }

    private OneOf<WingetGame, ErrorMessage> ParseRegistry(string id)
    {
        //_logger?.LogDebug("***ParseRegistry()");
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

            DateTime installDate = default;

            regKey.TryGetString("DisplayIcon", out var launch);
            regKey.TryGetString("DisplayName", out var name);
            regKey.TryGetString("HelpLink", out var help);
            if (regKey.TryGetString("InstallDate", out var date))
                DateTime.TryParseExact(date, "yyyyMMdd", new CultureInfo("en-US"), DateTimeStyles.None, out installDate);
            regKey.TryGetString("InstallLocation", out var path);
            regKey.TryGetString("Publisher", out var pub);
            regKey.TryGetString("UninstallString", out var uninst);
            regKey.TryGetString("URLInfoAbout", out var url);

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
        //_logger?.LogDebug("***GetPackageInfo()");
        //list header = "Name,Id,Version,Available,Source"
        //search header = "Name,Id,Version,Match,Source"

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

        if (strId.EndsWith('…'))
        {
            (name, strId, version) = Relist(name, strId, version);
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
        process.StartInfo.FileName = FindClient().GetFullPath();
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
                        strId = headLine[(headLine.LastIndexOf('[') + 1)..^2];
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

        //_logger?.LogDebug("> " + name);
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
