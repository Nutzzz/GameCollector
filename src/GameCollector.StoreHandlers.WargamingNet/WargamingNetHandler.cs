using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameCollector.StoreHandlers.WargamingNet;

/// <summary>
/// Handler for finding games installed with Wargaming.net Game Center.
/// </summary>
/// <remarks>
/// Uses files:
///   %ProgramData%\Wargaming.net\GameCenter\apps\
/// </remarks>
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
[PublicAPI]
public class WargamingNetHandler(IRegistry registry, IFileSystem fileSystem) : AHandler<WargamingNetGame, WargamingNetGameId>
{
    internal const string UninstRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <summary>
    /// The supported metadata version of this handler. You can change the version policy with
    /// <see cref="VersionPolicy"/>.
    /// </summary>
    public const float SupportedGameinfoVersion = 2.15f;

    /// <summary>
    /// The supported gameinfo version of this handler. You can change the version policy with
    /// <see cref="VersionPolicy"/>.
    /// </summary>
    public const float SupportedMetadataVersion = 6.9f; // 7.2f;

    /// <summary>
    /// Policy to use when the metadata or gameinfo version does not match <see cref="SupportedMetadataVersion"/>.
    /// The default behavior is <see cref="WargamingNet.VersionPolicy.Warn"/>.
    /// </summary>
    public VersionPolicy VersionPolicy { get; set; } = VersionPolicy.Warn;

    /// <inheritdoc/>
    public override IEqualityComparer<WargamingNetGameId>? IdEqualityComparer => WargamingNetGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<WargamingNetGame, WargamingNetGameId> IdSelector => game => game.AppId;

    /// <inheritdoc/>
    public override AbsolutePath FindClient()
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(UninstRegKey);
            if (regKey is not null)
            {
                if (regKey.TryGetString("DisplayIcon", out var icon))
                {
                    if (icon.Contains(',', StringComparison.Ordinal))
                        icon = icon[..icon.LastIndexOf(',')];
                    if (Path.IsPathRooted(icon))
                        return _fileSystem.FromUnsanitizedFullPath(icon);
                }
            }
        }

        return GetGameCenterPath().Combine("wgc.exe");
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code")]
    public override IEnumerable<OneOf<WargamingNetGame, ErrorMessage>> FindAllGames(Settings? settings = null)
    {
        List<string> appPaths = [];

        var appData = GetGameCenterPath().Combine("apps");
        if (!appData.DirectoryExists())
        {
            yield return new ErrorMessage($"The directory {appData.GetFullPath()} does not exist!");
            yield break;
        }

        var appFiles = appData.EnumerateFiles(Extension.None, recursive: true).ToArray();
        foreach (var appFile in appFiles)
        {
            using var stream = appFile.Read();
            using var reader = new StreamReader(stream);
            var line = reader.ReadLine(); // There should only be one line (the install location) in these files
            if (!string.IsNullOrEmpty(line))
                appPaths.Add(line);
        }

        foreach (var appPath in appPaths)
        {
            if (!Path.IsPathRooted(appPath))
                continue;

            var path = _fileSystem.FromUnsanitizedFullPath(appPath);

            var infoFile = path.Combine("game_info.xml");
            if (!infoFile.FileExists)
                continue;

            var id = "";
            var isInstalled = true;

            XmlSerializer infoSerializer = new(typeof(GameInfo));
            using (var stream = infoFile.Read())
            {
                var data = (GameInfo?)infoSerializer.Deserialize(stream);

                if (data is null || data.Game is null)
                    continue;

                if (!float.TryParse(data.Version, CultureInfo.InvariantCulture, out var gameinfoVersion))
                {
                    yield return new ErrorMessage($"File {infoFile.GetFullPath()} does not have a version!");
                    yield break;
                }
                var (versionMessage, isVersionError) = CreateVersionMessage(VersionPolicy, gameinfoVersion, SupportedGameinfoVersion, infoFile);
                if (versionMessage is not null)
                {
                    yield return new ErrorMessage(versionMessage);
                    if (isVersionError) yield break;
                }

                id = data.Game.Id ?? "";
                if (data.Game.Installed is not null && data.Game.Installed.Equals("false", StringComparison.OrdinalIgnoreCase))
                    isInstalled = false;
            }

            var metaFile = path.Combine("game_metadata").Combine("metadata.xml");
            if (!metaFile.FileExists)
                continue;

            var name = "";
            AbsolutePath exeFile = new();

            XmlSerializer metaSerializer = new(typeof(Metadata));
            using (var stream = metaFile.Read())
            {
                var data = (Metadata?)metaSerializer.Deserialize(stream);

                if (data is null || data.PredefinedSection is null)
                    continue;

                if (!float.TryParse(data.Version, CultureInfo.InvariantCulture, out var metadataVersion))
                {
                    yield return new ErrorMessage($"File {metaFile.GetFullPath()} does not have a version!");
                    yield break;
                }
                var (versionMessage, isVersionError) = CreateVersionMessage(VersionPolicy, metadataVersion, SupportedMetadataVersion, metaFile);
                if (versionMessage is not null)
                {
                    yield return new ErrorMessage(versionMessage);
                    if (isVersionError) yield break;
                }

                if (string.IsNullOrEmpty(id))
                    id = data.PredefinedSection.AppId;
                name = data.PredefinedSection.Name;

                if (data.PredefinedSection.Executables is not null)
                {
                    var strExe = "";
                    foreach (var exe in data.PredefinedSection.Executables)
                    {
                        if (!string.IsNullOrEmpty(exe.Arch) &&
                            exe.Arch.Equals("x64", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(exe.Exe))
                            {
                                strExe = exe.Exe;
                                break;
                            }
                        }
                        strExe = exe.Exe ?? "";
                    }
                    if (!string.IsNullOrEmpty(strExe))
                        exeFile = path.Combine(RelativePath.FromUnsanitizedInput(strExe));
                }
            }

            var (strIcon, strUninst) = ParseRegistry(path);

            AbsolutePath icon = new();
            if (!string.IsNullOrEmpty(strIcon))
            {
                if (strIcon.Contains(',', StringComparison.Ordinal))
                    strIcon = strIcon[..strIcon.LastIndexOf(',')];
                if (Path.IsPathRooted(strIcon))
                    icon = _fileSystem.FromUnsanitizedFullPath(strIcon);
            }
            else if (path != default)
                icon = path.Combine("game_metadata").Combine("game.ico");
            if (!icon.FileExists)
                icon = exeFile;

            //AbsolutePath launch = new();
            //var launchArgs = "";
            AbsolutePath uninst = new();
            var unArgs = "";
            if (!string.IsNullOrEmpty(strUninst))
            {
                if (strUninst.Contains(" --", StringComparison.Ordinal))
                {
                    var i = strUninst.IndexOf(" --", StringComparison.Ordinal);
                    unArgs = strUninst[(i + 1)..];
                    strUninst = strUninst[..i].Trim('\"');
                }
                if (Path.IsPathRooted(strUninst))
                    uninst = _fileSystem.FromUnsanitizedFullPath(strUninst);
                /*
                if (uninst.FileExists &&
                    uninst.FileName is not null &&
                    uninst.FileName.Equals("wgc_api.exe", StringComparison.OrdinalIgnoreCase))
                {
                    launch = uninst;
                    launchArgs = "--open";
                }
                */
            }

            yield return new WargamingNetGame(
                AppId: WargamingNetGameId.From(id ?? ""),
                Name: name ?? "",
                InstallLocation: path,
                Executable: exeFile,
                Icon: icon,
                Uninstall: uninst,
                UninstallArgs: unArgs,
                IsInstalled: isInstalled);
        }
    }

    private (string icon, string uninst) ParseRegistry(AbsolutePath path)
    {
        if (_registry is not null)
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var unKey = currentUser.OpenSubKey(UninstRegKey);
            {
                if (unKey is null)
                    return new();

                var strPath = path.GetFullPath();
                foreach (var subKeyName in unKey.GetSubKeyNames())
                {
                    if (string.IsNullOrEmpty(subKeyName) || subKeyName.Equals("Wargaming.net Game Center", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var subKey = currentUser.OpenSubKey(Path.Combine(UninstRegKey, subKeyName));
                    if (subKey is not null &&
                        subKey.TryGetString("InstallLocation", out var testPath) &&
                        strPath.Equals(testPath, StringComparison.OrdinalIgnoreCase))

                        return (subKey?.GetString("DisplayIcon") ?? "", subKey?.GetString("UninstallString") ?? "");
                }
            }
        }
        return new();
    }

    internal static (string? message, bool isError) CreateVersionMessage(
        VersionPolicy versionPolicy, float fileVersion, float supportedVersion, AbsolutePath filePath)
    {
        if (fileVersion == supportedVersion) return (null, false);

        return versionPolicy switch
        {
            VersionPolicy.Warn => (
                $"{filePath} has a file version " +
                $"{fileVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports version " +
                $"{supportedVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This message is a WARNING because the consumer of this library has set {nameof(VersionPolicy)} to {nameof(VersionPolicy.Warn)}",
                false),
            VersionPolicy.Error => (
                $"{filePath} has a file version " +
                $"{fileVersion.ToString(CultureInfo.InvariantCulture)} but this library only supports version " +
                $"{supportedVersion.ToString(CultureInfo.InvariantCulture)}. " +
                $"This is an ERROR because the consumer of this library has set {nameof(VersionPolicy)} to {nameof(VersionPolicy.Error)}",
                true),
            VersionPolicy.Ignore => (null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(versionPolicy), versionPolicy, message: null),
        };
    }

    public AbsolutePath GetGameCenterPath()
    {
        return _fileSystem.GetKnownPath(KnownPath.CommonApplicationDataDirectory)
                    .Combine("Wargaming.net")
                    .Combine("GameCenter");
    }
}
