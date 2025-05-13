using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameCollector.EmuHandlers.Dolphin;

/// <summary>
/// Handler for finding ROMs for Dolphin.
/// </summary>
/// <remarks>
/// Constructor.
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
/// <param name="dolphinPath"></param>
/// <param name="logger">Logger.</param>
[PublicAPI]
public partial class DolphinHandler(IRegistry registry, IFileSystem fileSystem, AbsolutePath dolphinPath, ILogger<DolphinHandler>? logger = null) : AHandler<DolphinGame, DolphinGameId>
{
    internal const string DolphinRegKey = @"Software\Dolphin Emulator";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly AbsolutePath _dolphinPath = dolphinPath;
    private readonly ILogger? _logger = logger ?? NullLogger<DolphinHandler>.Instance;

    /// <inheritdoc/>
    public override IEqualityComparer<DolphinGameId>? IdEqualityComparer => DolphinGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<DolphinGame, DolphinGameId> IdSelector => game => game.DolphinGameId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        return _dolphinPath;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<DolphinGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        // All games 
        if (!_dolphinPath.FileExists)
        {
            yield return new ErrorMessage($"The file {_dolphinPath} does not exist!");
            yield break;
        }

        var userPath = FindUserPath();
        _logger?.LogDebug("User path: {path}", userPath);
        /*
        List<string> files = new();
        foreach (var path in GetROMPaths(userPath, out var recurse))
        {
            files.AddRange(GetROMFiles(path, recurse));
        }
        */
        var romPaths = GetROMPaths(userPath, recurse: out var _);
        foreach (var game in ParseGameList(userPath, romPaths))
        {
            var file = game.File;
            if (!string.IsNullOrEmpty(file) && Path.IsPathFullyQualified(file))
            {
                var filePath = _fileSystem.FromUnsanitizedFullPath(file);
                if (filePath.FileExists)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var path = _fileSystem.FromUnsanitizedFullPath(filePath.Directory);
                    var id = game.GameId ?? name;
                    AbsolutePath icon = new();
                    if (id.Length > 5)
                        icon = userPath.Combine("Cache")
                            .Combine("GameCovers")
                            .Combine(string.Concat(id[..6], ".png"));
                    _ = DateTime.TryParse(game.ApploaderDate, CultureInfo.InvariantCulture, out var loaderDate);
                    yield return new DolphinGame(
                        DolphinGameId: DolphinGameId.From(id),
                        Title: game.Title ?? name,
                        Path: path,
                        DolphinExecutable: _dolphinPath,
                        ROMFile: file,
                        Cover: icon,
                        AppLoaderDate: loaderDate,
                        Publisher: game.Publisher,
                        System: game.System,
                        Region: game.Region);
                }
            }
        }
    }

    public AbsolutePath FindUserPath()
    {
        AbsolutePath userPath = default;
        AbsolutePath dolphinDir = default;

        if (_dolphinPath != default)
            dolphinDir = _fileSystem.FromUnsanitizedFullPath(_dolphinPath.Directory);

        if (dolphinDir != default && dolphinDir.Combine("portable.txt").FileExists)
        {
            userPath = dolphinDir.Combine("User");
            if (userPath.DirectoryExists())
                return userPath;
        }

        var userPathEnv = Environment.GetEnvironmentVariable("DOLPHIN_EMU_USERPATH");
        if (!string.IsNullOrEmpty(userPathEnv) && Path.IsPathFullyQualified(userPathEnv))
        {
            userPath = _fileSystem.FromUnsanitizedFullPath(userPathEnv);
            if (userPath.DirectoryExists())
                return userPath;
        }

        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);
            using var regKey = currentUser.OpenSubKey(DolphinRegKey);
            if (regKey is not null)
            {
                if (regKey.TryGetString("LocalUserConfig", out var local))
                {
                    if (local.Equals("1", StringComparison.Ordinal) && dolphinDir != default && dolphinDir.DirectoryExists())
                    {
                        userPath = dolphinDir.Combine("User");
                        if (userPath.DirectoryExists())
                            return userPath;
                    }
                }
                if (regKey.TryGetString("UserConfigPath", out var path) && Path.IsPathFullyQualified(path))
                {
                    userPath = _fileSystem.FromUnsanitizedFullPath(path);
                    if (userPath.DirectoryExists())
                        return userPath;
                }
            }
        }

        userPath = _fileSystem.GetKnownPath(KnownPath.MyDocumentsDirectory).Combine("Dolphin Emulator");
        if (userPath.DirectoryExists())
            return userPath;

        userPath = _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory).Combine("Dolphin Emulator");
        if (userPath.DirectoryExists())
            return userPath;

        if (dolphinDir != default && dolphinDir.DirectoryExists())
        {
            userPath = dolphinDir.Combine("User");
            if (userPath.DirectoryExists())
                return userPath;
        }

        return default;
    }

    // TODO: gamelist.cache parsing is pretty ugly; since Dolphin is open source, a neater solution is probably available
    internal IList<GameList> ParseGameList(AbsolutePath userPath, IList<string> romPaths)
    {
        GameList game = new();
        List<GameList> gameList = [];

        if (userPath == default)
            return gameList;

        var path = userPath.Combine("Cache").Combine("gamelist.cache");
        if (!path.FileExists)
        {
            _logger?.LogError("File not found: {path}", path);
            return gameList;
        }

        var bytes = File.ReadAllBytes(path.GetFullPath());
        var byteString = Encoding.UTF8.GetString(bytes);

        var i = 0;
        var j = 0;
        var start = false;
        var done = false;
        var num = 0;
        var prevStr = "";
        foreach (var str in byteString.Split("\0"))
        {
            if (string.IsNullOrEmpty(str) ||
                str.Length == 1 ||
                str.Length > 260)
                continue;
            i++;
            foreach (var romPath in romPaths)
            {
                if (str.StartsWith(romPath.Replace('\\', '/'), StringComparison.Ordinal))
                {
                    i = 0; j = 0; start = true; done = false; num++;
                    game = new() { File = str[..^1], System = (DolphinSystem)(-1) };
                    //_logger?.LogDebug("GAME# {num}", num);
                    //_logger?.LogDebug("[0] filepath: {str}", game.File);
                    break;
                }
            }
            if (done)
                continue;
            if (!start || i == 0)
                continue;
            if (i == 1)
            {
                //_logger?.LogDebug("[1] filename: {str}", str);
                continue;
            }
            if (i == 2 ||
                (i == 3 && game.System == (DolphinSystem)(-1)))
            {
                if (str.EndsWith('W'))
                {
                    game.System = DolphinSystem.GameCube;
                    //_logger?.LogDebug("[{i}] system: GameCube", i);
                    continue;
                }
                if (str.EndsWith("H0\u0002", StringComparison.Ordinal) ||
                    str.EndsWith("$\u0018\u0001", StringComparison.Ordinal) ||
                    str.EndsWith("�\u000f", StringComparison.Ordinal) ||
                    str.EndsWith("��\u0001", StringComparison.Ordinal))
                {
                    game.System = DolphinSystem.Wii;
                    //_logger?.LogDebug("[{i}] system: Wii", i);
                    continue;
                }
                if (i == 3 && game.System == (DolphinSystem)(-1))
                {
                    //_logger?.LogWarning("[3] unknown system: {str}", str);
                }
                continue;
            }

            switch (str[^1])
            {
                case '': //VT
                case '': //SO
                    game.Description = str[..^1];
                    //_logger?.LogDebug("[{i}] description: {str}", i, game.Description);
                    continue;
                case '': //SOH, hollow happyface
                case '': //STX, filled happyface
                case '': //ETX, heart
                case '': //EOT, diamond
                case '': //ENQ, club
                case '': //BEL
                    //_logger?.LogDebug("[{i}] title translation: {str}", i, str);
                    continue;
                case '': //ACK, spade
                    j++;
                    if (j > 1) // The gameid will be the last line ending in ACK/spade
                    {
                        game.GameId = str[..^1];
                        if (!string.IsNullOrEmpty(prevStr))
                        {
                            if (!char.IsAsciiLetter(prevStr[^1]))
                                game.Title = prevStr[..^1];
                            else
                                game.Title = prevStr;
                        }

                        // TODO: The best title seems to usually be the one prior to the gameid, but sometimes the one before that is preferable;
                        // I couldn't come up with a great way to make that determination automatically (e.g., the longer of the two, etc.)

                        //_logger?.LogDebug("[{i}] full title?: {prev} | gameid?: {str}", i, game.Title, str[..^1]);
                    }
                    if (!str.StartsWith("RVL", StringComparison.Ordinal)) // Skip this entry
                    {
                        //_logger?.LogDebug("[{i}] unknown ACK: {str}", i, str[..^1]);
                        prevStr = str;
                    }
                    //else
                    //    _logger?.LogWarning("[{i}] disregard?: {str}", i, str[..^1]);
                    continue;
                default:
                    break;
            }

            if (!string.IsNullOrEmpty(game.GameId) &&
                str.StartsWith(game.GameId, StringComparison.Ordinal))
            {
                game.GameId = str;
                //_logger?.LogDebug("[{i}] gameidext: {str}", i, game.GameId);
                game.System = (DolphinSystem)str[0];
                //game.TitleCode = $"{str[1]}{str[2]}";
                game.Region = (DolphinRegion)str[3];
                //game.PublisherCode = $"{str[4]}{str[5]}";
                continue;
            }

            var matches = DateRegex().Matches(str);
            if (matches.Count > 0)
            {
                game.ApploaderDate = matches[0].Value;
                //_logger?.LogDebug("[{i}] apploaderDate: {str}", i, game.ApploaderDate);
                done = true;
                gameList.Add(game);
                continue;
            }

            //_logger?.LogDebug("[{i}] unknown: {str}", i, str);
            prevStr = str;
        }

        return gameList;
    }

    /// <summary>
    /// Returns a list of filenames of the ROMs in the Dolphin SOFTWARE directories.
    /// </summary>
    private List<AbsolutePath> GetROMFiles(AbsolutePath path, bool recurse)
    {
        List<AbsolutePath> roms = [];

        _logger?.LogDebug("rompath: {path}", path);
        if (path.DirectoryExists())
        {
            var romFiles = _fileSystem.EnumerateFiles(path, "*.*", recurse).ToList();
            _logger?.LogDebug("{romcount} ROMs found", romFiles.Count);
            foreach (var romFile in romFiles)
            {
                roms.Add(romFile);
            }
        }
        return roms;
    }

    /// <summary>
    /// Retrieves absolute paths to the ROM directories as configured by Dolphin.
    /// </summary>
    private static List<string> GetROMPaths(AbsolutePath userPath, out bool recurse)
    {
        recurse = false;
        List<string> romPaths = [];

        if (userPath == default)
            return romPaths;

        using var stream = userPath.Combine("Config").Combine("Dolphin.ini").Read();
        using var reader = new StreamReader(stream);
        var line = "";
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("ISOPath", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("ISOPaths", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains('=', StringComparison.Ordinal))
                    romPaths.Add(line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..].Trim());
            }
            if (line.StartsWith("RecursiveISOPaths", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains('=', StringComparison.Ordinal) &&
                    line[..(line.IndexOf('=', StringComparison.Ordinal) + 1)].Trim().Equals("True", StringComparison.OrdinalIgnoreCase))
                    recurse = true;

            }
        }
        return romPaths;
    }

    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9]{2}")]
    private static partial Regex DateRegex();
}
