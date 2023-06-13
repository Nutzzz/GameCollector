using System;
using System.Collections.Generic;
using System.Globalization;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameFinder.Wine;

/// <summary>
/// Prefix manager for a vanilla Wine installation that searches for prefixes inside
/// the default locations.
/// </summary>
[PublicAPI]
public class DefaultWinePrefixManager : IWinePrefixManager<WinePrefix>
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fileSystem"></param>
    public DefaultWinePrefixManager(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public IEnumerable<OneOf<WinePrefix, LogMessage>> FindPrefixes()
    {
        foreach (var defaultWinePrefixLocation in GetDefaultWinePrefixLocations(_fileSystem))
        {
            if (!_fileSystem.DirectoryExists(defaultWinePrefixLocation)) continue;

            var res = IsValidPrefix(_fileSystem, defaultWinePrefixLocation);
            yield return res.Match<OneOf<WinePrefix, LogMessage>>(
                _ => new WinePrefix
                {
                    ConfigurationDirectory = defaultWinePrefixLocation,
                },
                message => message);
        }
    }

    internal static OneOf<bool, LogMessage> IsValidPrefix(IFileSystem fileSystem, AbsolutePath directory)
    {
        var virtualDrive = directory.CombineUnchecked("drive_c");
        if (!fileSystem.DirectoryExists(virtualDrive))
        {
            return new LogMessage($"Virtual C: drive does not exist at {virtualDrive}");
        }

        var systemRegistryFile = directory.CombineUnchecked("system.reg");
        if (!fileSystem.FileExists(systemRegistryFile))
        {
            return new LogMessage($"System registry file does not exist at {systemRegistryFile}");
        }

        var userRegistryFile = directory.CombineUnchecked("user.reg");
        if (!fileSystem.FileExists(userRegistryFile))
        {
            return new LogMessage($"User registry file does not exist at {userRegistryFile}");
        }

        return true;
    }

    internal static IEnumerable<AbsolutePath> GetDefaultWinePrefixLocations(IFileSystem fileSystem)
    {
        // from the docs: https://wiki.winehq.org/FAQ#Wineprefixes

        // ~/.wine is the default prefix
        yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory).CombineUnchecked(".wine");

        var winePrefixEnvVariable = Environment.GetEnvironmentVariable("WINEPREFIX");
        if (winePrefixEnvVariable is not null)
        {
            yield return fileSystem.FromFullPath(Utils.SanitizeInputPath(winePrefixEnvVariable));
        }

        // WINEPREFIX0, WINEPREFIX1, ...
        foreach (var numberedEnvVariable in GetNumberedEnvironmentVariables())
        {
            yield return fileSystem.FromFullPath(Utils.SanitizeInputPath(numberedEnvVariable));
        }

        // Bottling standards: https://wiki.winehq.org/Bottling_Standards
        // TODO: not sure which 3rd party applications actually use this
    }

    internal static IEnumerable<string> GetNumberedEnvironmentVariables()
    {
        for (var i = 0; i < 10; i++)
        {
            var envVariable = Environment
                .GetEnvironmentVariable($"WINEPREFIX{i.ToString(CultureInfo.InvariantCulture)}");
            if (envVariable is null) yield break;
            yield return envVariable;
        }
    }
}
