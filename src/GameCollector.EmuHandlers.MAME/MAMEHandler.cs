using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameCollector.EmuHandlers.MAME;

// Originally based on https://github.com/mika76/mamesaver
// Copyright (c) 2007 Contributors

/// <summary>
/// Handler for finding MAME ROMs.
/// </summary>
/// <remarks>
/// Constructor.
/// </remarks>
/// <param name="fileSystem">
/// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
/// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
/// a custom implementation or just a mock of the interface.
/// </param>
/// <param name="mamePath"></param>
/// <param name="logger">Logger.</param>
[PublicAPI]
public partial class MAMEHandler(IFileSystem fileSystem, AbsolutePath mamePath, ILogger<MAMEHandler>? logger = null) : AHandler<MAMEGame, MAMEGameId>
{
    /// <summary>
    ///     Number of ROMs to process per batch. ROM files are batched when passing as arguments to MAME both 
    ///     to minimize processing time and to allow a visual indicator that games are being processed.
    /// </summary>
    private const int ROMsPerBatch = 500;

    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly AbsolutePath _mamePath = mamePath;
    private readonly ILogger? _logger = logger ?? NullLogger<MAMEHandler>.Instance;

    private XmlReaderSettings _readerSettings = new();
    private event PropertyChangedEventHandler? _propertyChanged = null;
    //private PropertyChangedEventArgs _propertyChangedArgs = new("");
    private int _progress;

    /// <inheritdoc/>
    public override IEqualityComparer<MAMEGameId>? IdEqualityComparer => MAMEGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<MAMEGame, MAMEGameId> IdSelector => game => game.Name;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        return _mamePath;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<MAMEGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        return FindAllGames(settings?.InstalledOnly ?? false, settings?.BaseOnly ?? false);
    }

    /// <summary>
    /// Finds all ROMs supported by this emulator. The return type <see cref="ROMData"/>
    /// will always be a non-null game or a non-null error message.
    /// </summary>
    /// <param name="availableOnly">ROM file exists (and if doVerify is true, ROM is correct)</param>
    /// <param name="parentsOnly">Do not add clones</param>
    /// <param name="doVerify">Verify ROM is correct for MAME version (this is very slow)</param>
    /// <param name="minimumStatus">Minimum emulation status (from "good", "imperfect", or "preliminary")</param>
    /// <returns></returns>
    public IEnumerable<OneOf<MAMEGame, ErrorMessage>> FindAllGames(
        bool? availableOnly = false,
        bool? parentsOnly = false,
        bool doVerify = false,
        MachineStatus minimumStatus = MachineStatus.Imperfect)
    {
        //var commandLineArgs = "-skip_gameinfo -nowindow -noswitchres -sleep -triplebuffer";
        var commandLineArgs = "";
        _readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse };

        if (!_mamePath.FileExists)
        {
            yield return new ErrorMessage($"The file {_mamePath} does not exist!");
            yield break;
        }

        foreach (var game in GetGameList(progress => Progress = progress, availableOnly, parentsOnly, doVerify, minimumStatus))
        {
            if (!string.IsNullOrEmpty(game.Name) &&
                game.Machine is not null &&
                !string.IsNullOrEmpty(game.Machine.Description))
            {
                var displayType = "";
                var displayRotation = "";
                var players = "";
                var driverStatus = "";
                var failedToVerify = false;
                var statusNotMet = false;
                var gameCategory1 = "";
                var gameCategory2 = "";
                var gameCategory3 = "";
                var isMature = false;
                var isInstalled = false;
                var parent = "";

                if (!string.IsNullOrEmpty(game.Path))
                {
                    if (!doVerify)
                        isInstalled = true;
                    else
                    {
                        if (game.IsVerified)
                            isInstalled = true;
                        else if (!string.IsNullOrEmpty(game.Path))
                            failedToVerify = true;
                    }
                }

                parent = game.Machine.CloneOf;

                /*
                // these should have already been checked
                if (availableOnly && !isInstalled)
                    continue;
                if (parentsOnly && !string.IsNullOrEmpty(parent))
                    continue;
                */

                if (game.Machine.Display is not null)
                {
                    displayType = game.Machine.Display.Type;
                    displayRotation = game.Machine.Display.Rotate;
                }
                if (game.Machine.Input is not null)
                    players = game.Machine.Input.Players;

                if (game.Machine.Driver is not null)
                    driverStatus = game.Machine.Driver.Status;
                else
                    continue; // this should have already been checked

                if (!string.IsNullOrEmpty(driverStatus) &&
                    ToMachineStatus(driverStatus) < MachineStatus.Imperfect)
                    statusNotMet = true;

                if (game.Category is not null)
                {
                    gameCategory1 = game.Category.One;
                    gameCategory2 = game.Category.Two;
                    gameCategory3 = game.Category.Three;
                    isMature = game.Category.Mature;
                }

                List<Problem> problems = [];
                if (statusNotMet)
                    problems.Add(Problem.DoesNotMeetRequirements);
                if (failedToVerify)
                    problems.Add(Problem.FailedToVerify);

                yield return new MAMEGame(
                    Name: MAMEGameId.From(game.Name),
                    Description: game.Machine.Description,
                    Path: Path.IsPathRooted(game.Path) ? _fileSystem.FromUnsanitizedFullPath(game.Path) : new(),
                    MAMEExecutable: _mamePath,
                    CommandLineArgs: commandLineArgs,
                    Icon: _fileSystem.FromUnsanitizedFullPath(_mamePath.Directory).Combine("icons").Combine($"{game.Name}.ico"),
                    IsAvailable: isInstalled,
                    Problems: problems,
                    Parent: parent,
                    Year: game.Machine.Year,
                    Manufacturer: game.Machine.Manufacturer,
                    Categories: [gameCategory1 ?? "Unknown", gameCategory2 ?? "", gameCategory3 ?? ""],
                    IsMature: isMature,
                    Players: players,
                    DriverStatus: driverStatus,
                    DisplayType: displayType,
                    DisplayRotation: displayRotation,
                    VersionAdded: game.VerAdded
                );
            }
        }
    }

    /// <summary>
    ///     Returns a <see cref="List{T}" /> of <see cref="Machine" />s which are read from
    ///     the full list and then merged with the verified ROMs list. The games which are returned
    ///     all have a "good" status on their drivers. This check also eliminates BIOS ROMS.
    /// </summary>
    /// <param name="progressCallback">Callback invoked with percentage complete</param>
    /// <param name="availableOnly">ROM file exists (and if doVerify is true, ROM is correct)</param>
    /// <param name="parentsOnly">Do not add clones</param>
    /// <param name="doVerify">Verify ROM is correct for MAME version (this is very slow)</param>
    /// <param name="minimumStatus">Minimum emulation status (good, imperfect, preliminary, unknown)</param>
    /// <returns>Returns a <see cref="List{T}" /> of <see cref="Machine" />s</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code")]
    private List<ROMData> GetGameList(Action<int> progressCallback, bool? availableOnly, bool? parentsOnly, bool doVerify, MachineStatus minimumStatus)
    {
        CancellationToken cancelToken = new();
        var gameList = new List<ROMData>();

        XmlSerializer serializer = new(typeof(GameList));
        using var stream = MAMERunner.Run(_fileSystem, _mamePath, "-listxml").StandardOutput;
        var allGames = (GameList?)serializer.Deserialize(stream);
        if (allGames is null || allGames.Machines is null)
        {
            _logger?.LogError("Unable to retrieve game list");
            return gameList;
        }

        var build = allGames.Build;
        if (!string.IsNullOrEmpty(build) && build.Contains(' ', StringComparison.Ordinal))
            _logger?.LogInformation("MAME {build}", build[..build.IndexOf(' ', StringComparison.Ordinal)]);

        var romFiles = GetROMFiles();
        if (availableOnly == true && romFiles.Count == 0)
        {
            _logger?.LogDebug("No ROM files available.");
            return [];
        }

        var verifiedTask = GetVerifiedSets(progressCallback, romFiles, availableOnly, doVerify);
        verifiedTask.Wait(cancelToken);
        var verifiedGames = verifiedTask.Result.Keys;

        foreach (var game in allGames.Machines)
        {
            var path = "";
            if (string.IsNullOrEmpty(game.Name))
                continue;
            var name = game.Name;
            var isVerified = false;

            // Perform initial validity checks
            if (!IsGameValid(game, parentsOnly, minimumStatus))
                continue;

            foreach (var romFile in romFiles)
            {
                if (romFile.name.Equals(game.Name, StringComparison.OrdinalIgnoreCase))
                {
                    path = romFile.path;
                    break;
                }
            }

            if (availableOnly == true && string.IsNullOrEmpty(path))
            {
                _logger?.LogInformation("{name} not added to game list because a zip file could not be found", name);
                continue;
            }
            if (doVerify)
            {
                if (verifiedGames.Contains(game.Name, StringComparer.Ordinal))
                    isVerified = true;
                else if (availableOnly == true)
                {
                    _logger?.LogInformation("{name} not added to game list because its files could not be verified", name);
                    continue;
                }
            }

            // Enrich game metadata
            gameList.Add(new(name, game, CategoryParser.GetCategory(_mamePath, name, _fileSystem),
                CategoryParser.GetVersion(_mamePath, name, _fileSystem), path, isVerified));
        }

        return gameList;
    }

    /// <summary>
    ///     Performs preliminary validation checks against a game, verifying that it's runnable, isn't a BIOS
    ///     or mechanical, and matches whether clones are permitted and whether it has minimum emulation status
    /// </summary>
    /// <param name="machine">Class based on elements from MAME's <c>-listxml</c> output</param>
    /// <param name="parentsOnly">Do not add clones</param>
    /// <param name="minimumStatus">Minimum emulation status (from "good", "imperfect", or "preliminary")</param>
    private bool IsGameValid(Machine machine, bool? parentsOnly, MachineStatus minimumStatus)
    {
        if (!string.IsNullOrEmpty(machine.IsDevice) &&
            machine.IsDevice.Equals("yes", StringComparison.Ordinal))
            return false;

        // Verify that the ROM isn't a BIOS
        if (!string.IsNullOrEmpty(machine.IsBIOS) &&
            machine.IsBIOS.Equals("yes", StringComparison.Ordinal))
        {
            _logger?.LogDebug("{name} not added to game list as it is a BIOS", machine.Name);
            return false;
        }

        // Verify that the game isn't mechanical
        if (!string.IsNullOrEmpty(machine.IsMechanical) &&
            machine.IsMechanical.Equals("yes", StringComparison.Ordinal))
        {
            _logger?.LogDebug("{name} not added to game list as it is mechanical", machine.Name);
            return false;
        }

        // Verify that the game is runnable
        if (!string.IsNullOrEmpty(machine.Runnable) &&
            !machine.Runnable.Equals("yes", StringComparison.Ordinal))
        {
            _logger?.LogDebug("{name} not added to game list as it is not runnable", machine.Name);
            return false;
        }

        // Retrieve and verify driver and emulation status
        if (machine.Driver is null)
        {
            _logger?.LogInformation("{name} not added to game list as it has no driver specified", machine.Name);
            return false;
        }

        // Skip games which aren't sufficiently emulated
        var status = machine.Driver.Status;
        if (minimumStatus > MachineStatus.Preliminary &&
            !string.IsNullOrEmpty(status) &&
            ToMachineStatus(status) < minimumStatus)
        {
            _logger?.LogInformation("{name} not added to game list because it has a status of {status}", machine.Name, status);
            return false;
        }

        // Skip clones if parentsOnly flag
        if (parentsOnly == true && !string.IsNullOrEmpty(machine.CloneOf))
        {
            _logger?.LogInformation("{name} not added to game list as it is a clone of {parent}", machine.Name, machine.CloneOf);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Returns a <see cref="Dictionary{TKey,TValue}" /> filled with the names of games which are
    ///     verified to work. Only the ones marked as good are returned. The clone names
    ///     are returned in the value of the dictionary while the name is used as the key.
    /// </summary>
    private async Task<Dictionary<string, string>> GetVerifiedSets(Action<int> progressCallback, List<(string, string)> romFiles, bool? availableOnly, bool doVerify)
    {
        var verifiedROMs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!doVerify)
            return verifiedROMs;

        // Verify each ROM in directory
        var index = 0;
        var filesProcessed = 0;

        List<string> filesToVerify;
        while ((filesToVerify = romFiles.Select(n => n.Item1).ToList().Skip(index).Take(ROMsPerBatch).ToList()).Count != 0)
        {
            var arguments = new List<string> { "-verifyroms" }.Concat(filesToVerify).ToArray();
            using (var stream = MAMERunner.Run(_fileSystem, _mamePath, arguments).StandardOutput)
            {
                var output = stream.ReadToEnd();

                var matches = GoodRegex().Matches(output);
                for (var i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    verifiedROMs[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }

            index += ROMsPerBatch;
            filesProcessed += filesToVerify.Count;

            var percentage = Callback(progressCallback, filesProcessed, romFiles.Count);
            if (percentage < 100)
                _logger?.LogDebug("MAME verify ROMs: {progress}%", percentage.ToString(CultureInfo.InvariantCulture));
        }

        return verifiedROMs;
    }

    /// <summary>
    ///     Invokes the callback with the percentage of game list construction completed.
    /// </summary>
    /// <param name="callback">callback to invoke</param>
    /// <param name="processed">number of ROM files processed</param>
    /// <param name="romCount">total number of ROM files</param>
    private static int Callback(Action<int> callback, float processed, int romCount)
    {
        var percentage = (int)Math.Round(processed / romCount * 100);

        callback(percentage);
        return percentage;
    }

    /// <summary>
    ///     Returns a list of the base name of ROMs in the MAME ROM directories.
    /// </summary>
    /// <remarks>
    ///     It is assumed that ROMs are zipped.
    /// </remarks>
    private List<(string name, string path)> GetROMFiles()
    {
        List<(string name, string path)> roms = [];

        foreach (var path in GetROMPaths())
        {
            _logger?.LogDebug("rompath: {path}", path);
            List<string> romFiles = [];
            if (Path.IsPathRooted(path) && _fileSystem.FromUnsanitizedFullPath(path).DirectoryExists())
            {
                romFiles = [.. Directory.GetFiles(path, "*.zip")];
                _logger?.LogDebug("{romcount} ROMs found", romFiles.Count);
                foreach (var romFile in romFiles)
                {
                    roms.Add((name: Path.GetFileNameWithoutExtension(romFile), path));
                }
            }
        }

        return roms;
    }

    /// <summary>
    ///     Retrieves absolute paths to the ROM directories as configured by MAME.
    /// </summary>
    /// <remarks>
    ///     Multiple ROM directories can be specified in MAME by separating directories by semicolons.
    /// </remarks>
    private List<string> GetROMPaths() => GetConfigPaths("rompath");

    /// <summary>
    ///     Returns the value of a path element in the <c>mame.ini</c> file.
    /// </summary>
    /// <param name="key">key name</param>
    /// <returns>list of absolute paths</returns>
    private List<string> GetConfigPaths(string key)
    {
        using var stream = MAMERunner.Run(_fileSystem, _mamePath, "-showconfig").StandardOutput;
        var line = stream.ReadLine();
        while (line is not null)
        {
            if (line.StartsWith(key, StringComparison.Ordinal))
                return new(ExtractConfigPaths(key, line));

            line = stream.ReadLine();
        }

        _logger?.LogError("Unable to retrieve {key} path", key);
        return [];
    }

    /// <summary>
    ///     Emulation status of games which are added to the game list.
    /// </summary>
    private static MachineStatus ToMachineStatus(string status)
    {
        return status switch
        {
            "good" => MachineStatus.Good,
            "imperfect" => MachineStatus.Imperfect,
            "preliminary" => MachineStatus.Preliminary,
            _ => (MachineStatus)(-1),
        };
    }

    /// <summary>
    ///     Game list rebuilding percentage completion.
    /// </summary>
    public int Progress
    {
        get => _progress;
        set
        {
            if (value == _progress) return;
            _progress = value;
            OnPropertyChanged();
        }
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnAllPropertiesChanged()
    {
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
    }

    /// <summary>
    ///     Parses a MAME configuration line into a collection of directories.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="line">Entire configuration line to parse</param>
    /// <returns>Absolute directories extracted from <c>line</c>, or <langword>null</langword> if no match</returns>
    private List<string> ExtractConfigPaths(string key, string line)
    {
        // Extract path string from configuration line
        var directoryRegex = new Regex($@"{key}\s+(.+)");
        var match = directoryRegex.Match(line);

        // Remove any quotation marks around entire path collection and split into individual paths
        var rawPaths = CleansePath(match.Groups[1].Value);
        var paths = rawPaths.Split(';');

        // Construct list of absolute paths
        var absolutePaths = new List<string>();
        foreach (var path in paths.Select(CleansePath))
        {
            // If path is absolute, add raw path
            if (Path.IsPathRooted(path))
                absolutePaths.Add(path);
            else
            {
                // If path is relative, construct absolute path relative to MAME executable
                var workingDir = _mamePath.Directory;
                if (!string.IsNullOrEmpty(workingDir))
                    absolutePaths.Add(Path.Combine(workingDir, path));
            }
        }

        return absolutePaths;
    }

    /// <summary>
    ///     Cleanses a single path or list of paths, trimming whitespace and removing surrounding quotation marks. 
    /// </summary>
    private string CleansePath(string path)
    {
        path = path.Trim();

        // Remove surrounding quotation marks from path string. These are added by MAME if spaces are 
        // present in any part of the path.
        return QuotedRegex().IsMatch(path) ? QuotedRegex().Match(path).Groups[1].Value : path;
    }

    [GeneratedRegex(@"romset (\w*)(?:\s\[(\w*)\])? is good")]
    private static partial Regex GoodRegex();
    [GeneratedRegex(@"^\""(.*)\""$")]
    private static partial Regex QuotedRegex();
}
