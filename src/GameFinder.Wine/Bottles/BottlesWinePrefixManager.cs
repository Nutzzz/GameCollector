using System.Collections.Generic;
using System.Linq;
using GameFinder.Common;
using NexusMods.Paths;
using OneOf;

namespace GameFinder.Wine.Bottles;

/// <summary>
/// Wineprefix manager for prefixes created and managed by Bottles.
/// </summary>
public class BottlesWinePrefixManager : IWinePrefixManager<BottlesWinePrefix>
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fs"></param>
    public BottlesWinePrefixManager(IFileSystem fs)
    {
        _fileSystem = fs;
    }

    /// <inheritdoc/>
    public IEnumerable<OneOf<BottlesWinePrefix, LogMessage>> FindPrefixes()
    {
        var defaultLocation = GetDefaultLocations(_fileSystem)
            .FirstOrDefault(x => _fileSystem.DirectoryExists(x));

        if (string.IsNullOrEmpty(defaultLocation.Directory))
        {
            yield return new LogMessage("Unable to find any bottles installation.");
            yield break;
        }

        var bottles = defaultLocation.CombineUnchecked("bottles");
        foreach (var bottle in _fileSystem.EnumerateDirectories(bottles, recursive: false))
        {
            var res = IsValidBottlesPrefix(_fileSystem, bottle);
            yield return res.Match<OneOf<BottlesWinePrefix, LogMessage>>(
                _ => new BottlesWinePrefix
                {
                    ConfigurationDirectory = bottle,
                },
                message => message);
        }
    }

    internal static OneOf<bool, LogMessage> IsValidBottlesPrefix(IFileSystem fs, AbsolutePath directory)
    {
        var defaultWinePrefixRes = DefaultWinePrefixManager.IsValidPrefix(fs, directory);
        if (defaultWinePrefixRes.IsMessage())
        {
            return defaultWinePrefixRes.AsMessage();
        }

        var bottlesConfigFile = directory.CombineUnchecked("bottle.yml");
        if (!fs.FileExists(bottlesConfigFile))
        {
            return new LogMessage($"Bottles configuration file is missing at {bottlesConfigFile}");
        }

        return true;
    }

    internal static IEnumerable<AbsolutePath> GetDefaultLocations(IFileSystem fs)
    {
        // $XDG_DATA_HOME/bottles aka ~/.local/share/bottles
        yield return fs.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .CombineUnchecked("bottles");

        // ~/.var/app/com.usebottles.bottles/data/bottles (flatpak installation)
        // https://github.com/flatpak/flatpak/wiki/Filesystem
        yield return fs.GetKnownPath(KnownPath.HomeDirectory)
            .CombineUnchecked(".var/app/com.usebottles.bottles/data/bottles");
    }
}
