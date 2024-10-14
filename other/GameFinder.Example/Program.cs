using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameCollector.Launcher.Heroic;
using GameCollector.StoreHandlers.EADesktop;
using GameCollector.StoreHandlers.EADesktop.Crypto;
using GameCollector.StoreHandlers.EADesktop.Crypto.Windows;
using GameCollector.StoreHandlers.EGS;
using GameCollector.StoreHandlers.GOG;
using GameCollector.StoreHandlers.Origin;
using GameCollector.StoreHandlers.Steam;
using GameCollector.StoreHandlers.Xbox;
using GameCollector.Wine;
using GameCollector.Wine.Bottles;
using GameCollector.StoreHandlers.Amazon;
using GameCollector.StoreHandlers.Arc;
using GameCollector.StoreHandlers.BattleNet;
using GameCollector.StoreHandlers.BigFish;
using GameCollector.StoreHandlers.GameJolt;
using GameCollector.StoreHandlers.Humble;
using GameCollector.StoreHandlers.IGClient;
using GameCollector.StoreHandlers.Itch;
using GameCollector.StoreHandlers.Legacy;
using GameCollector.StoreHandlers.Oculus;
using GameCollector.StoreHandlers.Paradox;
using GameCollector.StoreHandlers.Plarium;
using GameCollector.StoreHandlers.Riot;
using GameCollector.StoreHandlers.RobotCache;
using GameCollector.StoreHandlers.Rockstar;
using GameCollector.StoreHandlers.Ubisoft;
using GameCollector.StoreHandlers.WargamingNet;
using GameCollector.PkgHandlers.Winget;
using GameCollector.EmuHandlers.Dolphin;
using GameCollector.EmuHandlers.MAME;
//using GameCollector.DataHandlers.TheGamesDb;
using Microsoft.Extensions.Logging;
using NexusMods.Paths;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using OneOf;
using FileSystem = NexusMods.Paths.FileSystem;
using IFileSystem = NexusMods.Paths.IFileSystem;
using ILogger = Microsoft.Extensions.Logging.ILogger;

[assembly: ExcludeFromCodeCoverage]
namespace GameCollector;

public static class Program
{
    private static NLogLoggerProvider _provider = null!;

    public static void Main(string[] args)
    {
        var config = new LoggingConfiguration();

        var coloredConsoleTarget = new ColoredConsoleTarget("coloredConsole")
        {
            DetectConsoleAvailable = true,
            EnableAnsiOutput = OperatingSystem.IsLinux(), // windows hates this
            UseDefaultRowHighlightingRules = false,
            WordHighlightingRules =
            {
                new ConsoleWordHighlightingRule
                {
                    Regex = @"\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d+",
                    CompileRegex = true,
                    ForegroundColor = ConsoleOutputColor.Gray,
                },
                new ConsoleWordHighlightingRule("DEBUG", ConsoleOutputColor.Gray, ConsoleOutputColor.NoChange),
                new ConsoleWordHighlightingRule("INFO", ConsoleOutputColor.Cyan, ConsoleOutputColor.NoChange),
                new ConsoleWordHighlightingRule("ERROR", ConsoleOutputColor.Red, ConsoleOutputColor.NoChange),
                new ConsoleWordHighlightingRule("WARNING", ConsoleOutputColor.Yellow, ConsoleOutputColor.NoChange),
            },
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message:withexception=true}",
        };

        var fileTarget = new FileTarget("file")
        {
            FileName = "log.log",
        };

        config.AddRuleForAllLevels(coloredConsoleTarget);
        config.AddRuleForAllLevels(fileTarget);

        LogManager.Configuration = config;
        _provider = new NLogLoggerProvider();

        var logger = _provider.CreateLogger(nameof(Program));

        Parser.Default
            .ParseArguments<Options>(args)
            .WithParsed(x => Run(x, logger));
    }

    private async static void Run(Options options, ILogger logger)
    {
        var realFileSystem = FileSystem.Shared;
        CancellationTokenSource cancelSource = new();
        var cancelToken = cancelSource.Token;
        List<Task> tasks = new();

        var logFile = realFileSystem.GetKnownPath(KnownPath.CurrentDirectory).Combine("log.log");
        if (realFileSystem.FileExists(logFile)) realFileSystem.DeleteFile(logFile);

        logger.LogInformation("Operating System: {OSDescription}", RuntimeInformation.OSDescription);

        var settings = new Settings(options.Installed, options.Parent || options.Base, options.Owned, options.Games);

        if (options.All) // Enable all handlers
        {
            options.Amazon = true;
            options.Arc = true;
            options.BattleNet = true;
            options.BigFish = true;
            //options.Dolphin ??= "";       // needs path
            options.EA = true;
            options.Epic = true;
            options.GameJolt = true;
            options.GOG = true;
            options.Heroic = true;
            options.Humble = true;
            options.IG = true;
            options.Itch = true;
            options.Legacy = true;
            //options.MAME ??= "";          // needs path
            options.Oculus = true;
            options.Origin = true;
            options.Paradox = true;
            options.Plarium = true;
            options.Riot = true;
            options.RobotCache = true;
            options.Rockstar = true;
            options.Steam = true;
            //options.TheGamesDB = true;    // WIP
            options.Ubisoft = true;
            options.Wargaming = true;
            options.Winget = true;
            options.Xbox = true;
        }

        if (OperatingSystem.IsWindows())
        {
            var windowsRegistry = WindowsRegistry.Shared;
            if (options.Steam) RunSteamHandler(settings, realFileSystem, windowsRegistry, options.SteamAPI);
            //if (options.Steam) tasks.Add(Task.Run(() => RunSteamHandler(settings, realFileSystem, windowsRegistry, options.SteamAPI), cancelToken));
            if (options.GOG) tasks.Add(Task.Run(() => RunGOGHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Epic || options.EGS) RunEGSHandler(settings, windowsRegistry, realFileSystem);
            //if (options.Epic || options.EGS) tasks.Add(Task.Run(() => RunEGSHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Origin) tasks.Add(Task.Run(() => RunOriginHandler(settings, realFileSystem), cancelToken));
            if (options.Xbox) tasks.Add(Task.Run(() => RunXboxHandler(settings, realFileSystem), cancelToken));
            if (options.EA || options.EADesktop)
            {
                tasks.Add(Task.Run(() =>
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var hardwareInfoProvider = new HardwareInfoProvider();
                    var decryptionKey = Decryption.CreateDecryptionKey(new HardwareInfoProvider());
                    var sDecryptionKey = Convert.ToHexString(decryptionKey).ToLower(CultureInfo.InvariantCulture);
                    logger.LogDebug("EA Decryption Key: {DecryptionKey}", sDecryptionKey);

                    RunEADesktopHandler(settings, realFileSystem, windowsRegistry, hardwareInfoProvider);
                }, cancelToken));
            }
            if (options.Amazon) tasks.Add(Task.Run(() => RunAmazonHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Arc) tasks.Add(Task.Run(() => RunArcHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.BattleNet || options.Blizzard) tasks.Add(Task.Run(() => RunBattleNetHandler(settings, realFileSystem), cancelToken));
            if (options.BigFish) tasks.Add(Task.Run(() => RunBigFishHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.GameJolt) tasks.Add(Task.Run(() => RunGameJoltHandler(settings, realFileSystem), cancelToken));
            if (options.Humble) tasks.Add(Task.Run(() => RunHumbleHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.IG || options.Indiegala || options.IGClient) tasks.Add(Task.Run(() => RunIGClientHandler(settings, realFileSystem), cancelToken));
            if (options.Itch) tasks.Add(Task.Run(() => RunItchHandler(settings, realFileSystem), cancelToken));
            if (options.Legacy) tasks.Add(Task.Run(() => RunLegacyHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Oculus) tasks.Add(Task.Run(() => RunOculusHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Paradox) tasks.Add(Task.Run(() => RunParadoxHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Plarium) tasks.Add(Task.Run(() => RunPlariumHandler(settings, realFileSystem), cancelToken));
            if (options.Riot) tasks.Add(Task.Run(() => RunRiotHandler(settings, realFileSystem), cancelToken));
            if (options.RobotCache) tasks.Add(Task.Run(() => RunRobotCacheHandler(settings, realFileSystem), cancelToken));
            if (options.Rockstar) tasks.Add(Task.Run(() => RunRockstarHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Ubisoft || options.Uplay) tasks.Add(Task.Run(() => RunUbisoftHandler(settings, windowsRegistry, realFileSystem), cancelToken));
            if (options.Wargaming || options.WargamingNet) tasks.Add(Task.Run(() => RunWargamingNetHandler(settings, windowsRegistry, realFileSystem), cancelToken));

            if (options.Winget) tasks.Add(Task.Run(() => RunWingetHandler(settings, windowsRegistry, realFileSystem), cancelToken));

            if (options.Dolphin is not null)
            {
                tasks.Add(Task.Run(() =>
                {
                    if (Path.IsPathRooted(options.Dolphin))
                    {
                        var path = realFileSystem.FromUnsanitizedFullPath(options.Dolphin);
                        RunDolphinHandler(settings, windowsRegistry, realFileSystem, path);
                    }
                    else
                        logger.LogError("Bad Dolphin path {DolphinPath}", options.Dolphin);
                }, cancelToken));
            }

            if (options.MAME is not null)
            {
                tasks.Add(Task.Run(() =>
                {
                    if (Path.IsPathRooted(options.MAME))
                    {
                        var path = realFileSystem.FromUnsanitizedFullPath(options.MAME);
                        RunMAMEHandler(settings, realFileSystem, path);
                    }
                    else
                        logger.LogError("Bad MAME path {MAMEPath}", options.MAME);
                }, cancelToken));
            }
        }

        if (OperatingSystem.IsLinux())
        {
            if (options.Steam) tasks.Add(Task.Run(() => RunSteamHandler(settings, realFileSystem, registry: null, options.SteamAPI), cancelToken));
            if (options.Heroic) tasks.Add(Task.Run(() => RunHeroicGOGHandler(realFileSystem), cancelToken));

            tasks.Add(Task.Run(() =>
            {
                var winePrefixes = new List<AWinePrefix>();
                if (options.Wine)
                {
                    var prefixManager = new DefaultWinePrefixManager(realFileSystem);
                    winePrefixes.AddRange(LogWinePrefixes(prefixManager, _provider.CreateLogger("Wine")));
                }

                if (options.Bottles)
                {
                    var prefixManager = new BottlesWinePrefixManager(realFileSystem);
                    winePrefixes.AddRange(LogWinePrefixes(prefixManager, _provider.CreateLogger("Bottles")));
                }

                foreach (var winePrefix in winePrefixes)
                {
                    var wineFileSystem = winePrefix.CreateOverlayFileSystem(realFileSystem);
                    var wineRegistry = winePrefix.CreateRegistry(realFileSystem);

                    if (options.GOG) RunGOGHandler(settings, wineRegistry, wineFileSystem);
                    if (options.Epic) RunEGSHandler(settings, wineRegistry, wineFileSystem);
                    if (options.Origin) RunOriginHandler(settings, wineFileSystem);
                    if (options.Xbox) RunXboxHandler(settings, wineFileSystem);
                }
            }, cancelToken));
        }

        if (OperatingSystem.IsMacOS())
        {
            if (options.Steam)
                RunSteamHandler(settings, realFileSystem, registry: null, options.SteamAPI);
        }

        //if (options.TheGamesDB) tasks.Add(Task.Run(() => RunTheGamesDbHandler(settings, realFileSystem, options.TheGamesDBAPI), cancelToken));

        Task.WaitAll(tasks.ToArray(), cancelToken);

        /*
        Parallel.ForEach(tasks, task =>
        {
            task.Start();
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        */

        logger.LogInformation($"{nameof(Program)} complete");
    }

    private static void RunGOGHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(GOGHandler));
        var handler = new GOGHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunHeroicGOGHandler(IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(HeroicGOGHandler));
        var handler = new HeroicGOGHandler(fileSystem);
        LogGamesAndErrors(handler.FindAllGames(), logger);
    }

    private static void RunEGSHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(EGSHandler));
        var handler = new EGSHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunOriginHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(OriginHandler));
        var handler = new OriginHandler(fileSystem, registry: null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunEADesktopHandler(
        Settings settings,
        IFileSystem fileSystem,
        IRegistry registry,
        IHardwareInfoProvider hardwareInfoProvider)
    {
        var logger = _provider.CreateLogger(nameof(EADesktopHandler));
        var handler = new EADesktopHandler(fileSystem, registry, hardwareInfoProvider);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCodeAttribute",
        Justification = "Required types are preserved using TrimmerRootDescriptor file.")]
    private static void RunXboxHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(XboxHandler));
        var handler = new XboxHandler(fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunSteamHandler(
        Settings settings,
        IFileSystem fileSystem,
        IRegistry? registry = null,
        string? steamAPI = null)
    {
        var logger = _provider.CreateLogger(nameof(SteamHandler));
        var handler = new SteamHandler(fileSystem, registry, steamAPI);
        LogGamesAndErrors(handler.FindAllGames(settings), logger, game =>
        {
            if (!OperatingSystem.IsLinux()) return;
            var protonPrefix = game.GetProtonPrefix();
            if (protonPrefix is null) return;
            logger.LogInformation("Proton Directory for this game: {}", protonPrefix.ProtonDirectory.GetFullPath());
        });
    }

    private static void RunAmazonHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(AmazonHandler));
        var handler = new AmazonHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunArcHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(ArcHandler));
        var handler = new ArcHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunBattleNetHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(BattleNetHandler));
        var handler = new BattleNetHandler(fileSystem, null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunBigFishHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(BigFishHandler));
        var handler = new BigFishHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunGameJoltHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(GameJoltHandler));
        var handler = new GameJoltHandler(fileSystem, registry: null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunHumbleHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(HumbleHandler));
        var handler = new HumbleHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunIGClientHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(IGClientHandler));
        var handler = new IGClientHandler(fileSystem, registry: null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunItchHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(ItchHandler));
        var handler = new ItchHandler(fileSystem, registry: null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunLegacyHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(LegacyHandler));
        var handler = new LegacyHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunOculusHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(OculusHandler));
        var handler = new OculusHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunParadoxHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(ParadoxHandler));
        var handler = new ParadoxHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunPlariumHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(PlariumHandler));
        var handler = new PlariumHandler(fileSystem, registry: null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunRiotHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(RiotHandler));
        var handler = new RiotHandler(fileSystem, registry: null);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunRobotCacheHandler(Settings settings, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(RobotCacheHandler));
        var handler = new RobotCacheHandler(fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunRockstarHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(RockstarHandler));
        var handler = new RockstarHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunUbisoftHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(UbisoftHandler));
        var handler = new UbisoftHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunWargamingNetHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(WargamingNetHandler));
        var handler = new WargamingNetHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }
    private static void RunWingetHandler(Settings settings, IRegistry registry, IFileSystem fileSystem)
    {
        var logger = _provider.CreateLogger(nameof(WingetHandler));
        var handler = new WingetHandler(registry, fileSystem);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunDolphinHandler(Settings settings, IRegistry registry, IFileSystem fileSystem, AbsolutePath path)
    {
        var logger = _provider.CreateLogger(nameof(DolphinHandler));
        var handler = new DolphinHandler(registry, fileSystem, path); //, logger);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    private static void RunMAMEHandler(Settings settings, IFileSystem fileSystem, AbsolutePath path)
    {
        var logger = _provider.CreateLogger(nameof(MAMEHandler));
        var handler = new MAMEHandler(fileSystem, path, registry: null); //, logger);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }

    /*
    private static async void RunTheGamesDbHandler(IFileSystem fileSystem, string? tgdbApi, Settings settings)
    {
        var logger = _provider.CreateLogger(nameof(TheGamesDbHandler));
        //var handler = new TheGamesDbHandler(fileSystem, tgdbApi, registry: null, logger);
        var handler = new TheGamesDbHandler(fileSystem, registry: null, logger);
        LogGamesAndErrors(handler.FindAllGames(settings), logger);
    }
    */

    private static List<AWinePrefix> LogWinePrefixes<TWinePrefix>(IWinePrefixManager<TWinePrefix> prefixManager, ILogger logger)
    where TWinePrefix : AWinePrefix
    {
        var res = new List<AWinePrefix>();

        foreach (var result in prefixManager.FindPrefixes())
        {
            result.Switch(prefix =>
            {
                logger.LogInformation("Found wine prefix at {PrefixConfigurationDirectory}", prefix.ConfigurationDirectory);
                res.Add(prefix);
            }, error =>
            {
                logger.LogError("{Error}", error);
            });
        }
        logger.LogInformation("{num} prefixes found.", res.Count);

        return res;
    }

    private static void LogGamesAndErrors<TGame>(IEnumerable<OneOf<TGame, ErrorMessage>> results, ILogger logger, Action<TGame>? action = null)
        where TGame : class
    {
        var numGames = 0;
        foreach (var result in results)
        {
            result.Switch(game =>
            {
                numGames++;
                logger.LogInformation("Found {Game}", game);
                action?.Invoke(game);
            }, error =>
            {
                logger.LogError("{Error}", error);
            });
        }
        logger.LogInformation("{num} games found.", numGames);
    }
}
