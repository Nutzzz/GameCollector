using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text.Json;
using System.Text;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;
using static System.Environment;

namespace GameCollector.StoreHandlers.Oculus;

/// <summary>
/// Handler for finding games installed with Oculus.
/// </summary>
[PublicAPI]
public class OculusHandler : AHandler<OculusGame, string>
{
    internal const string OculusRegKey = @"SOFTWARE\Oculus VR, LLC\Oculus";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

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
    public OculusHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override IEqualityComparer<string>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override Func<OculusGame, string> IdSelector => game => game.GameId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var localMachine32 = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var regKey = localMachine32.OpenSubKey(Path.Combine(OculusRegKey));
            if (regKey is not null)
            {
                if (regKey.TryGetString("Base", out var basePath) && Path.IsPathRooted(basePath))
                    return _fileSystem.FromFullPath(SanitizeInputPath(basePath))
                        .CombineUnchecked("Support")
                        .CombineUnchecked("oculus-client")
                        .CombineUnchecked("OculusClient.exe");
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override IEnumerable<OneOf<OculusGame, ErrorMessage>> FindAllGames(bool installedOnly = false, bool baseOnly = false)
    {
        List<OneOf<OculusGame, ErrorMessage>> games = new();

        var restartSvc = false;

        // Stop service (otherwise database is locked)
        if (OperatingSystem.IsWindows())
            restartSvc = StopService();

        List<string> libPaths = new();
        Dictionary<string, string> exePaths = new();

        var database = _fileSystem.GetKnownPath(KnownPath.ApplicationDataDirectory)
            .CombineUnchecked("Oculus")
            .CombineUnchecked("sessions")
            .CombineUnchecked("_oaf")
            .CombineUnchecked("data.sqlite");

        if (!database.FileExists)
            return new OneOf<OculusGame, ErrorMessage>[] { new ErrorMessage("Oclus database not found") };

        var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);
        using (var libKey = currentUser.OpenSubKey(@"Software\Oculus VR, LLC\Oculus\Libraries"))
        {
            if (libKey is not null)
            {
                foreach (var lib in libKey.GetSubKeyNames())
                {
                    using var subKey = currentUser.OpenSubKey(Path.Combine(@"Software\Oculus VR, LLC\Oculus\Libraries", lib));
                    if (subKey is not null && subKey.TryGetString("OriginalPath", out var path))
                        libPaths.Add(path);
                }
            }
        }

        foreach (var lib in libPaths)
        {
            List<string> libFiles = new();
            try
            {
                var manifestPath = Path.Combine(lib, "Manifests");
                libFiles = Directory.GetFiles(manifestPath, "*.json.mini", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception e)
            {
                games.Add(new ErrorMessage(e, $"Exception reading directory {lib}"));
                continue;
            }

            foreach (var file in libFiles)
            {
                try
                {
                    var strDocumentData = File.ReadAllText(file);

                    if (!string.IsNullOrEmpty(strDocumentData))
                    {
                        using var document = JsonDocument.Parse(@strDocumentData, new() { AllowTrailingCommas = true, });
                        document.RootElement.TryGetProperty("canonicalName", out var jName);
                        document.RootElement.TryGetProperty("launchFile", out var jLaunch);
                        if (document.RootElement.TryGetProperty("appId", out var jId))
                            exePaths.Add(jId.ToString(), Path.Combine(lib, "Software", jName.ToString(), jLaunch.ToString()));
                    }
                }
                catch (Exception e)
                {
                    games.Add(new ErrorMessage(e, $"Malformed file {file}"));
                }
            }
        }

        try
        {
            CultureInfo ci = new("en-US");
            var ti = ci.TextInfo;
            //ulong userId = 0;

            using SQLiteConnection con = new($"Data Source={database.GetFullPath()}");
            con.Open();

            // Get the user ID to check entitlements for expired trials
            /*
            using (SQLiteCommand cmdU = new("SELECT hashkey, value FROM Objects WHERE typename = 'User'", con))
            {
                using SQLiteDataReader rdrU = cmdU.ExecuteReader();
                while (rdrU.Read())
                {
                    byte[] valU = new byte[rdrU.GetBytes(1, 0, null, 0, int.MaxValue) - 1];
                    rdrU.GetBytes(1, 0, valU, 0, valU.Length);
                    string strValU = Encoding.Default.GetString(valU);

                    string alias = ParseBlob(strValU, "alias", "app_entitlements");
                    if (string.IsNullOrEmpty(userName) || userName.Equals("skipped"))
                    {
                        if (ulong.TryParse(rdrU.GetString(0), out userId))
                        {
                            userName = alias;
                            break;
                        }
                    }
                    else if (userName.Equals(alias, CDock.IGNORE_CASE))
                    {
                        ulong.TryParse(rdrU.GetString(0), out userId);
                        break;
                    }
                }
            }
            */

            using SQLiteCommand cmd = new("SELECT hashkey, value FROM Objects WHERE typename = 'Application'", con);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var displayName = "";
                var strDescription = "";
                List<string> genres = new();
                var strLaunch = "";

                var url = "";
                var hasProblem = false;
                /*
                var exePath = "", exePath2d = "", exeParams = "", exeParams2d = "";
                var state = "", time = "";
                */

                if (!ulong.TryParse(rdr.GetString(0), out var id) || id == 3082125255194578) // Rift
                    continue;

                var val = new byte[rdr.GetBytes(1, 0, null, 0, int.MaxValue) - 1];
                rdr.GetBytes(1, 0, val, 0, val.Length);
                var strVal = Encoding.Default.GetString(val);

                _ = ulong.TryParse(ParseBlob(strVal, "ApplicationAssetBundle", "can_access_feature_keys", -1, 0), out var assets);
                //ulong.TryParse(ParseBlob(strVal, "PCBinary", "livestreaming_status", -1, 0), out ulong bin);
                var name = ParseBlob(strVal, "canonical_name", "category");
                displayName = ParseBlob(strVal, "display_name", "display_short_description");
                if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(displayName))
                    displayName = ti.ToTitleCase(name.Replace('-', ' '));

                strDescription = ParseBlob(strVal, "display_short_description", "genres");
                var strGenres = ParseBlob(strVal, "genres", "grouping", 1);
                var genreArray = strGenres.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                foreach (var genre in genreArray)
                {
                    genres.Add(genre[..^1]);
                }

                using (SQLiteCommand cmd2 = new($"SELECT value FROM Objects WHERE hashkey = '{assets}'", con))
                {
                    using var rdr2 = cmd2.ExecuteReader();
                    while (rdr2.Read())
                    {
                        var val2 = new byte[rdr2.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                        rdr2.GetBytes(0, 0, val2, 0, val2.Length);
                        var strVal2 = Encoding.Default.GetString(val2);
                        url = ParseBlob(strVal2, "uri", "version_code", strStart1: "size");
                    }
                }

                // The exe's can be gotten from the .json files, which we have to get anyway to figure out the install path
                /*
                using (SQLiteCommand cmd3 = new($"SELECT value FROM Objects WHERE hashkey = '{bin}'", con))
                {
                    using var rdr3 = cmd3.ExecuteReader();
                    while (rdr3.Read())
                    {
                        var val3 = new byte[rdr3.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                        rdr3.GetBytes(0, 0, val3, 0, val3.Length);
                        var strVal3 = Encoding.Default.GetString(val3);
                        exePath = ParseBlob(strVal3, "launch_file", "launch_file_2d");
                        exePath2d = ParseBlob(strVal3, "launch_file_2d", "launch_parameters");
                        exeParams = ParseBlob(strVal3, "launch_parameters", "launch_parameters_2d");
                        exeParams2d = ParseBlob(strVal3, "launch_parameters_2d", "manifest_signature");
                    }
                }

                if (userId > 0)
                {
                    // TODO: If this is an expired trial, count it as not-installed
                    using SQLiteCommand cmd4 = new($"SELECT value FROM Objects WHERE hashkey = '{userId}:{id}'", con);
                    using var rdr4 = cmd4.ExecuteReader();
                    while (rdr4.Read())
                    {
                        var val4 = new byte[rdr4.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                        rdr4.GetBytes(0, 0, val4, 0, val4.Length);
                        var strVal4 = Encoding.Default.GetString(val4);
                        state = ParseBlob(strVal4, "active_state", "expiration_time");
                        if (!state.Equals("PERMANENT"))
                        {
                            time = ParseBlob(strVal4, "expiration_time", "grant_reason");
                            CLogger.LogDebug($"expiry: {state} {time}");
                            //if (!...expired)
                            hasProblem = true;
                        }
                    }
                }
                */

                if (exePaths.ContainsKey(id.ToString()))
                {
                    AbsolutePath launch = new();
                    strLaunch = SanitizeInputPath(exePaths[id.ToString()]);
                    if (Path.IsPathRooted(strLaunch))
                        launch = _fileSystem.FromFullPath(strLaunch);
                    games.Add(new OculusGame(
                        HashKey: OculusGameId.From(id),
                        DisplayName: displayName,
                        InstallPath: Path.IsPathRooted(launch.Directory) ? _fileSystem.FromFullPath(launch.Directory) : default,
                        LaunchFile: launch,
                        IsInstalled: true,
                        HasProblem: hasProblem,
                        Description: strDescription,
                        Genres: genres,
                        CanonicalName: name
                        ));
                }
                else
                {
                    games.Add(new OculusGame(
                        HashKey: OculusGameId.From(id),
                        DisplayName: displayName,
                        InstallPath: new(),
                        IsInstalled: false,
                        CanonicalName: name
                        ));
                }
            }
            con.Close();
        }
        catch (Exception e)
        {
            games.Add(new ErrorMessage(e, $"Exception parsing database file {database}"));
        }

        // TODO: Should we bother with starting the service back up?
        //if (OperatingSystem.IsWindows() && restartSvc)
        //    sc.Start();

        return games;
    }

    private static string ParseBlob(string strVal, string strStart, string strEnd, int startAdjust = 0, int stopAdjust = 0, string strStart1 = "")
    {
        if (!string.IsNullOrEmpty(strStart1))
        {
            var start1 = strVal.IndexOf(strStart1, StringComparison.Ordinal);
            if (start1 > 0)
                strVal = strVal[start1..];
        }
        var start = strVal.IndexOf(strStart, StringComparison.Ordinal);
        var stop = strVal.IndexOf(strEnd, StringComparison.Ordinal);
        if (start > 0 && stop > start)
        {
            start += strStart.Length + 10 + startAdjust;
            stop -= 5 + stopAdjust;
            if (stop - start < 1)
                stop = start + 1;
            return strVal[start..stop];
        }
        return "";
    }

    [SupportedOSPlatform("windows")]
    public bool StopService(string serviceName = "OVRService")
    {
        ServiceController sc = new(serviceName);
        try
        {
            if (sc.Status.Equals(ServiceControllerStatus.Running) || sc.Status.Equals(ServiceControllerStatus.StartPending))
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped);
                return true;
            }
        }
        catch (Exception) { }

        return false;
    }
}