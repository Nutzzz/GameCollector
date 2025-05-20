using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
//using System.Security.Principal;
using System.Text;
using GameCollector.Common;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.PkgHandlers.Choco;

/// <summary>
/// Handler for finding installed apps via Chocolatey.
/// </summary>
/// <remarks>
/// Constructor. Leverages choco, which inspects local Chocolatey database for installed programs.
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
public class ChocoHandler(IRegistry registry, IFileSystem fileSystem, ILogger? logger = null) : AHandler<ChocoGame, ChocoGameId>
{
    internal const string DefaultQuery1 = "game";
    internal const string DefaultQuery2 = "games";
    internal const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    internal const string InstallLocation = " InstallLocation:";
    internal const string Uninstall = " Uninstall:";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger _logger = logger ?? NullLogger<ChocoHandler>.Instance;

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
    public override IEqualityComparer<ChocoGameId>? IdEqualityComparer => ChocoGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<ChocoGame, ChocoGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        AbsolutePath chocoExe;
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';').ToList();
        if (paths is not null && paths.Count > 0)
        {
            foreach (var path in paths)
            {
                if (path.TrimEnd('\\').EndsWith(Path.Combine("chocolatey", "bin"), StringComparison.OrdinalIgnoreCase))
                {
                    chocoExe = _fileSystem.FromUnsanitizedFullPath(Path.Combine(path, "choco.exe"));
                    if (chocoExe.FileExists)
                        return chocoExe;
                    break;
                }
            }
        }
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        chocoExe = _fileSystem.FromUnsanitizedFullPath(Path.Combine(programData, "chocolatey", "bin", "choco.exe"));
        if (Path.IsPathFullyQualified(programData) && chocoExe.FileExists)
            return chocoExe;

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<ChocoGame, ErrorMessage>> FindAllGames(Settings? settings = null)
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
    /// <param name="queries">Search for tag(s) in online database (defaults to "game" and "games"); installedOnly must not be true</param>
    /// <param name="expandPackage">Get additional information about choco packages from online database</param>
    /// <returns></returns>
    public IEnumerable<OneOf<ChocoGame, ErrorMessage>> FindAllGames(
        bool installedOnly = false,
        bool ownedOnly = false,
        bool gamesOnly = false,
        bool storeOnly = false,
        IList<string>? queries = null,
        bool expandPackage = false)
    {
        queries ??= [DefaultQuery1, DefaultQuery2];

        Dictionary<ChocoGameId, OneOf<ChocoGame, ErrorMessage>> remoteGames = [];
        Dictionary<ChocoGameId, OneOf<ChocoGame, ErrorMessage>> installedPkgs = [];
        List<OneOf<ChocoGame, ErrorMessage>> allPackages = [];

        // TODO: Get install location: parse log, or fall back to searching under C:\ProgramData\chocolatey\bin\
        // Log: C:\ProgramData\chocolatey\logs\choco.summary.log
        //   or C:\ProgramData\chocolatey\logs\chocolatey.log
        // If using fallback, note that some files under bin\ are shims to a different location
        // PS: (yourprogram --shimgen-noop | Select-String $a) -split $a | ForEach-Object Trim
        installedPkgs = GetInstalled(expandPackage);

        if (!installedOnly)
        {
            foreach (var query in queries)
            {
                Dictionary<int, string> testme = [];
                var testthis = testme.ToList();
                SearchRemote(query, expandPackage).ToList().ForEach(x => remoteGames.TryAdd(x.Key, x.Value));
            }
        }

        foreach (var package in GetInstalled(expandPackage))
        {
            if (remoteGames.ContainsKey(package.Key))
            {
                if (package.Value.IsT0 && remoteGames[package.Key].IsT0)
                {
                    yield return new ChocoGame(
                        Id: package.Key,
                        Title: package.Value.AsT0.Title,
                        InstallLocation: package.Value.AsT0.InstallLocation,
                        Uninstall: package.Value.AsT0.Uninstall,
                        UninstallArgs: package.Value.AsT0.UninstallArgs,
                        IsInstalled: true,
                        InstalledVersion: package.Value.AsT0.InstalledVersion,
                        DefaultVersion: remoteGames[package.Key].AsT0.DefaultVersion,
                        PublishDate: remoteGames[package.Key].AsT0.PublishDate,
                        Notes: remoteGames[package.Key].AsT0.Notes,
                        RemoveOnly: remoteGames[package.Key].AsT0.RemoveOnly,
                        Approved: remoteGames[package.Key].AsT0.Approved,
                        Cached: remoteGames[package.Key].AsT0.Cached,
                        Broken: remoteGames[package.Key].AsT0.Broken
                    );
                }
                else
                    yield return package.Value.AsT1;
            }
            else
                yield return package.Value;
        }
        foreach (var package in remoteGames)
        {
            if (!installedPkgs.ContainsKey(package.Key))
                yield return package.Value;
        }

        if (!storeOnly)
        {
            foreach (var install in GetInstalledWindows())
            {
                yield return install;
            }
        }
    }

    private IEnumerable<OneOf<ChocoGame, ErrorMessage>> GetInstalledWindows()
    {
        var chocoExe = FindClient();
        if (chocoExe == default)
        {
            _logger?.LogDebug("***Choco not installed");
            yield return new ErrorMessage("Choco not installed");
        }

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = chocoExe.GetFullPath();
        process.StartInfo.Arguments = "list null --exact --include-programs --no-color --verbose";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var i = 0;
        foreach (var line in output.Split('\n'))
        {
            var title = "";
            var version = "";
            AbsolutePath location = default;
            AbsolutePath uninstall = default;
            var uninstallArgs = "";
            if (i == 0 ||
                string.IsNullOrEmpty(line) ||
                line.StartsWith("[NuGet]", StringComparison.OrdinalIgnoreCase) ||
                line.EndsWith("packages installed.", StringComparison.OrdinalIgnoreCase)) // The list we're looking for is after this line.
            {
                i++; continue;
            }

            if (line.StartsWith(' '))
            {
                //" InstallLocation: "
                if (line.StartsWith(InstallLocation, StringComparison.OrdinalIgnoreCase))
                {
                    var locStr = line[InstallLocation.Length..];
                    if (Path.IsPathFullyQualified(locStr))
                        location = _fileSystem.FromUnsanitizedFullPath(locStr);
                }
                //" Uninstall:"
                else if (line.StartsWith(Uninstall, StringComparison.OrdinalIgnoreCase))
                {
                    var uninstStr = line[Uninstall.Length..];
                    if (uninstStr.StartsWith('"'))
                        uninstallArgs = uninstStr[..uninstStr.IndexOf('"', 2)];
                    else if (uninstStr.Contains(' ', StringComparison.Ordinal))
                        uninstallArgs = uninstStr[..uninstStr.IndexOf(' ', StringComparison.Ordinal)];
                    if (!string.IsNullOrEmpty(uninstallArgs))
                        uninstStr = uninstStr[..uninstStr.IndexOf('"', StringComparison.Ordinal)];
                    if (Path.IsPathFullyQualified(uninstStr))
                        uninstall = _fileSystem.FromUnsanitizedFullPath(uninstStr);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(title))
                {
                    yield return new ChocoGame(ChocoGameId.From(title), title, location, Uninstall: uninstall, UninstallArgs: uninstallArgs);
                    location = default;
                    uninstall = default;
                    uninstallArgs = "";
                }
                if (line.Contains('|', StringComparison.Ordinal))
                {
                    title = line[..line.LastIndexOf('|')];
                    version = line[line.LastIndexOf('|')..];
                }
            }

            i++;
        }

        yield return new();
    }

    private Dictionary<ChocoGameId, OneOf<ChocoGame, ErrorMessage>> GetInstalled(bool expandPackage)
    {
        //*************************************
        Console.OutputEncoding = Encoding.UTF8;
        //*************************************

        Dictionary<ChocoGameId, OneOf<ChocoGame, ErrorMessage>> installed = [];

        var chocoExe = FindClient();
        if (chocoExe == default)
        {
            _logger?.LogDebug("***Choco not installed");
            return new() { [ChocoGameId.From("")] = new ErrorMessage("Choco not installed") };
        }

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = chocoExe.GetFullPath();
        process.StartInfo.Arguments = "list --no-color --verbose";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            _logger?.LogDebug("***No output from " + chocoExe.GetFullPath() + process.StartInfo.Arguments);
            return new() { [ChocoGameId.From("")] = new ErrorMessage("No output from " + chocoExe.GetFullPath() + process.StartInfo.Arguments) };
        }

        List<int> colPos = [];
        var i = 0;
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith(' '))
            {
                if (line.StartsWith(" TitleName ", StringComparison.Ordinal))
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
                ChocoGameId id = default;
                // TODO: This doesn't work if there are surrogate pairs in the string
                if (line.Length > colPos[1])
                    name = line[colPos[0]..colPos[1]].Trim();
                if (line.Length > colPos[2])
                {
                    strId = line[colPos[1]..colPos[2]].Trim();
                    id = ChocoGameId.From(strId);
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
                        if (installed.TryAdd(id, new ChocoGame(
                            Id: id,
                            Title: name,
                            InstallLocation: default,
                            IsInstalled: true,
                            InstalledVersion: version)))
                        {
                            _logger?.LogDebug("  " + name);
                        }
                        i++;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(strId) && !strId.EndsWith('…'))
                    {
                        var game = ParseRegistry(strId);
                        if (game.IsT0)
                        {
                            if (installed.TryAdd(id, new ChocoGame(
                                Id: id,
                                Title: name,
                                InstallLocation: game.AsT0.InstallLocation,
                                //Launch: game.AsT0.Launch,
                                Uninstall: game.AsT0.Uninstall,
                                IsInstalled: true,
                                //InstallDate: game.AsT0.InstallDate,
                                //SupportUrl: game.AsT0.SupportUrl,
                                //Homepage: game.AsT0.Homepage,
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

                    if (installed.TryAdd(id, new ChocoGame(
                        Id: id,
                        Title: name,
                        InstallLocation: default,
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
                    if (installed.TryAdd(id, new ChocoGame(
                        Id: id,
                        Title: name,
                        InstallLocation: default,
                        IsInstalled: true,
                        //Source: source,
                        PkgTags: new(),
                        InstalledVersion: version,
                        DefaultVersion: available)))
                    {
                        _logger?.LogDebug("  " + name);
                    }
                    i++;
                    continue;
                }

                if (installed.TryAdd(id, GetPackageInfo(search: false)))
                {
                    _logger?.LogDebug("  " + name);
                }
            }

            i++;
        }
        _logger?.LogDebug("GetInstalled(): " + i.ToString(CultureInfo.InvariantCulture) + " apps");

        return installed;
    }

    private Dictionary<ChocoGameId, OneOf<ChocoGame, ErrorMessage>> SearchRemote(string query = DefaultQuery1, bool expandPackage = true)
    {
        Dictionary<ChocoGameId, OneOf<ChocoGame, ErrorMessage>> freeGames = [];

        var chocoExe = FindClient();

        using var process = new Process();
        process.StartInfo = _startInfo;
        process.StartInfo.FileName = chocoExe.GetFullPath();
        //if (expandPackage)
            process.StartInfo.Arguments = $"search {query} --by-tag-only";
        //else
        //    process.StartInfo.Arguments = $"search {query} --by-tag-only --limit-output";
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            _logger?.LogDebug("***No output from " + chocoExe.GetFullPath() + process.StartInfo.Arguments);
            return new() { [ChocoGameId.From("")] = new ErrorMessage("No output from " + chocoExe.GetFullPath() + process.StartInfo.Arguments) };
        }

        foreach (var pkg in ParsePackages(output, expandPackage))
        {
            if (pkg.IsT0)
                _ = freeGames.TryAdd(ChocoGameId.From(pkg.AsT0.GameId), pkg);
            else
                _ = freeGames.TryAdd(ChocoGameId.From(""), pkg);
        }

        return freeGames;
    }

    private IEnumerable<OneOf<ChocoGame, ErrorMessage>> ParsePackages(string input, bool expandPackage = true)
    {
        var i = 0;
        ChocoGameId id = default;
        bool? remove = null;
        var version = "";
        ulong? numDownloads;
        ulong? numVerDownloads;
        DateTime? pubDate = default;
        bool? approved = null;
        bool? cached = null;
        bool? broken = null;
        var notes = "";

        foreach (var line in input.Split('\n'))
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("[NuGet]", StringComparison.OrdinalIgnoreCase))
            {
                i++; continue;
            }

            if (line.EndsWith("packages found.", StringComparison.OrdinalIgnoreCase) ||     // [search]
                line.EndsWith("packages installed.", StringComparison.OrdinalIgnoreCase))   // [list]
                break;

            if (expandPackage && line.StartsWith(' '))
            {
                //" Title: " ... " | Published: "
                const string TITLE = " Title:";
                const string PUBLISHED = "Published:";
                const string REMOVE_ONLY = "(remove only)";
                if (line.StartsWith(TITLE, StringComparison.OrdinalIgnoreCase))
                {
                    var title = "";
                    if (line.Contains('|', StringComparison.Ordinal))
                    {
                        title = line[(TITLE.Length + 1)..line.IndexOf('|', StringComparison.Ordinal)];
                        if (line.Contains(PUBLISHED, StringComparison.OrdinalIgnoreCase))
                        {
                            var iPub = line.IndexOf(PUBLISHED, StringComparison.OrdinalIgnoreCase) + PUBLISHED.Length + 1;
                            pubDate = line[iPub..].ToNullableDateTimeCurrent();
                        }
                    }
                    else
                        title = line[(TITLE.Length + 1)..];
                    if (title.EndsWith(REMOVE_ONLY, StringComparison.OrdinalIgnoreCase))
                    {
                        title = title[..(title.IndexOf(REMOVE_ONLY, StringComparison.OrdinalIgnoreCase) - 1)];
                        remove = true;
                    }
                }

                //" Package approved as a trusted package on " [search]
                //  OR
                //" Package approved by " ... " on " [search]
                //" Package testing status: " ... " on " [search]

                //" Number of Downloads: " ... " | Downloads for this version: "
                const string NUM_DOWNLOADS = " Number of Downloads:";
                const string NUM_VER_DOWNLOADS = "Downloads for this version:";
                if (line.StartsWith(NUM_DOWNLOADS, StringComparison.OrdinalIgnoreCase))
                {
                    var strDls = "";
                    strDls = line[(NUM_DOWNLOADS.Length + 1)..line.IndexOf('|', StringComparison.Ordinal)];
                    var iDls = line.IndexOf(NUM_VER_DOWNLOADS, StringComparison.OrdinalIgnoreCase) + NUM_VER_DOWNLOADS.Length + 1;
                    numDownloads = line[iDls..].ToNullableULong();
                    if (line.Contains('|', StringComparison.Ordinal))
                    {
                        strDls = line[(NUM_DOWNLOADS.Length + 1)..line.IndexOf('|', StringComparison.Ordinal)];
                        if (line.Contains(NUM_VER_DOWNLOADS, StringComparison.OrdinalIgnoreCase))
                        {
                            iDls = line.IndexOf(NUM_VER_DOWNLOADS, StringComparison.OrdinalIgnoreCase) + NUM_VER_DOWNLOADS.Length + 1;
                            numVerDownloads = line[iDls..].ToNullableULong();
                        }
                    }
                    else
                        numDownloads = line[(NUM_DOWNLOADS.Length + 1)..].ToNullableULong();
                }
                //" Package url "
                //" Chocolatey Package Source: "
                //" Package Checksum: " [search]
                //" Tags: "
                //" Software Site: "
                //" Software License: "
                //" Software Source: "
                //" Documentation: "
                //" Mailing List: "
                //" Issues: "
                //" Summary: "
                //" Description: "
                //" Release Notes: "
                //" Remembered Package Arguments:"
            }
            else if (i > 0) // && !line.StartsWith("Chocolatey v", StringComparison.Ordinal))
            {
                if (i > 1)
                {
                    yield return new ChocoGame(
                        Id: id,
                        Title: line,
                        InstallLocation: default,
                        IsInstalled: false,
                        DefaultVersion: version,
                        PublishDate: pubDate,
                        Notes: notes,
                        RemoveOnly: remove,
                        Approved: approved,
                        Cached: cached,
                        Broken: broken
                    );

                    id = default;
                    version = "";
                    pubDate = default;
                    notes = "";
                    remove = null;
                    approved = null;
                    cached = null;
                    broken = null;
                }

                var j = 0;
                var strId = "";
                foreach (var ch in line)
                {
                    j++;
                    if (char.IsWhiteSpace(ch))
                        break;
                    strId += ch;

                }
                id = ChocoGameId.From(strId);
                foreach (var ch in line[j..])
                {
                    j++;
                    if (char.IsWhiteSpace(ch))
                        break;
                    version += ch;
                }
                if (line.Length > j + 1)
                {
                    notes = line[(j + 1)..];
                    if (notes.Contains("[Approved]", StringComparison.Ordinal))
                        approved = true;
                    if (notes.Contains("cached", StringComparison.Ordinal))
                        cached = true;
                    if (notes.Contains("broken", StringComparison.Ordinal))
                        broken = true;
                }
            }
            i++;
        }
        _logger?.LogDebug("ParsePackages(): " + i.ToString(CultureInfo.InvariantCulture) + " games");
    }

    private OneOf<ChocoGame, ErrorMessage> ParseRegistry(string id)
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

            return new ChocoGame(
                Id: ChocoGameId.From(id),
                Title: name,
                InstallLocation: Path.IsPathFullyQualified(path) ? _fileSystem.FromUnsanitizedFullPath(path) : new(),
                //InstallDate: installDate == default ? null : installDate,
                //Launch: Path.IsPathFullyQualified(launch) ? _fileSystem.FromUnsanitizedFullPath(launch) : new(),
                Uninstall: Path.IsPathFullyQualified(uninst) ? _fileSystem.FromUnsanitizedFullPath(uninst) : new(),
                //Publisher: pub,
                //SupportUrl: help,
                SoftwareSite: url
            );
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {regKeyName}");
        }
    }

    private OneOf<ChocoGame, ErrorMessage> GetPackageInfo(bool search = false)
    {
        var chocoExe = FindClient();

        var isDescription = false;
        var isReleaseNotes = false;

        ChocoGameId id;
        var strId = "";
        var version = "";
        var publisher = "";
        var tags = "";
        /*
        var id = listInfo[colPos[0]..colPos[1]].Trim();
        var strId = listInfo[colPos[1]..colPos[2]].Trim();
        var id = ChocoGameId.From(strId);
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
        */

        var title = "";
        //var publisherUrl = "";
        var publisherSupportUrl = "";
        var summary = "";
        var description = "";
        var homepage = "";
        var license = "";
        //var licenseUrl = "";
        //var privacyUrl = "";
        //var copyright = "";
        var releaseNotes = "";
        //var releaseNotesUrl = "";
        //var purchaseUrl = "";
        //var documentationTutorials = "";
        //var documentationManual = "";
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
        process.StartInfo.FileName = chocoExe.GetFullPath();
        process.StartInfo.Arguments = $"search {strId} --exact --verbose --no-color";
        process.Start();
        var pkgOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var i = 0;
        foreach (var pkgLine in pkgOutput.Split('\n'))
        {
            if (isDescription)
            {
                if (pkgLine.StartsWith("  ", StringComparison.Ordinal))
                {
                    description += pkgLine.TrimStart();
                    continue;
                }
                isDescription = false;
            }
            if (isReleaseNotes)
            {
                if (pkgLine.StartsWith("  ", StringComparison.Ordinal))
                {
                    releaseNotes += pkgLine.TrimStart();
                    continue;
                }
                isReleaseNotes = false;
            }

            if (i == 0)
            {
                if (pkgLine.StartsWith("Found ", StringComparison.Ordinal))
                {
                    var headLine = pkgLine[pkgLine.IndexOf("Found ", StringComparison.Ordinal)..];
                    if (headLine.LastIndexOf('[') > 7)
                    {
                        strId = headLine[(headLine.LastIndexOf('[') + 1)..^1];
                        id = ChocoGameId.From(strId);
                        title = headLine["Found ".Length..headLine.LastIndexOf('[')];
                    }
                    else
                        title = headLine["Found ".Length..];
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
            else if (pkgLine.StartsWith(" Summary:", StringComparison.Ordinal))
                summary = pkgLine[" Summary:".Length..].TrimStart();
            else if (pkgLine.StartsWith(" Description:", StringComparison.Ordinal))
            {
                description = pkgLine[" Description:".Length..].TrimStart();
                isDescription = true;
            }
            else if (pkgLine.StartsWith("Homepage: ", StringComparison.Ordinal))
                homepage = pkgLine["Homepage: ".Length..];
            else if (pkgLine.StartsWith(" Software License:", StringComparison.Ordinal))
                license = pkgLine[" Software License: ".Length..];
            else if (pkgLine.StartsWith(" Release Notes:", StringComparison.Ordinal))
            {
                releaseNotes = pkgLine[" Release Notes: ".Length..].TrimStart();
                isReleaseNotes = true;
            }
            else if (pkgLine.StartsWith(" Tags:", StringComparison.Ordinal))
                tags = pkgLine[" Tags: ".Length..];
            else if (pkgLine.StartsWith("  Installer Url: ", StringComparison.Ordinal))
                installerUrl = pkgLine["  Installer Url: ".Length..];

            i++;
        }

        _logger?.LogDebug("> " + title);
        return new ChocoGame(
            Id: ChocoGameId.From(strId),
            Title: title,
            InstallLocation: default,
            //InstallerUrl: installerUrl,
            IsInstalled: true,
            Description: description,
            //PkgTags: tags,
            SoftwareSite: homepage,
            //SupportUrl: publisherSupportUrl,
            //PkgSource: source,
            SoftwareLicense: license,
            InstalledVersion: version
            //DefaultVersion: available,
            //AgeRating: agreementsAgeRatings
            );
    }
}
