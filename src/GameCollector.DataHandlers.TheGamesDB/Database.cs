using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace GameCollector.DataHandlers.TheGamesDB;

public enum Status
{
    Unknown = -1,
    Success,
}

public enum ArtType
{
    Unknown = -1,
    Banner,
    Boxart,
    Clearlogo,
    Fanart,
    Screenshot,
    Titlescreen,
}

public enum ArtSide
{
    Unknown = -1,
    Back,
    Front,
}

public enum Coop
{
    Unknown = -1,
    No,
    Yes,
}

public enum Genre
{
    [Description("Action")]
    Action = 1,
    [Description("Adventure")]
    Adventure = 2,
    [Description("Construction and Management Simulation")]
    ConstructionAndManagementSimulation = 3,
    [Description("Role-Playing")]
    RolePlaying = 4,
    [Description("Puzzle")]
    Puzzle = 5,
    [Description("Strategy")]
    Strategy = 6,
    [Description("Racing")]
    Racing = 7,
    [Description("Shooter")]
    Shooter = 8,
    [Description("Life Simulation")]
    LifeSimulation = 9,
    [Description("Fighting")]
    Fighting = 10,
    [Description("Sports")]
    Sports = 11,
    [Description("Sandbox")]
    Sandbox = 12,
    [Description("Flight Simulator")]
    FlightSimulator = 13,
    [Description("MMO")]
    MMO = 14,
    [Description("Platform")]
    Platform = 15,
    [Description("Stealth")]
    Stealth = 16,
    [Description("Music")]
    Music = 17,
    [Description("Horror")]
    Horror = 18,
    [Description("Vehicle Simulation")]
    VehicleSimulation = 19,
    [Description("Board")]
    Board = 20,
    [Description("Education")]
    Education = 21,
    [Description("Family")]
    Family = 22,
    [Description("Party")]
    Party = 23,
    [Description("Productivity")]
    Productivity = 24,
    [Description("Quiz")]
    Quiz = 25,
    [Description("Utility")]
    Utility = 26,
    [Description("Virtual Console")]
    VirtualConsole = 27,
    [Description("Unofficial")]
    Unofficial = 28,
    [Description("GBA Video / PSP Video")]
    GBAVideo_PSPVideo = 29,
}

public enum Rating
{
    [Description("NR - Not Rated")]
    NotRated,
    [Description("EC - Early Childhood")]
    EarlyChildhood,
    [Description("E - Everyone")]
    Everyone,
    [Description("E10+ - Everyone 10+")]
    Everyone10Plus,
    [Description("T - Teen")]
    Teen,
    [Description("M - Mature 17+")]
    Mature17Plus,
    [Description("AO - Adult Only 18+")]
    AdultsOnly18Plus,
    [Description("RP - Rating Pending")]
    RatingPending,
}

public enum Platform
{
    [Description("PC")]
    PC = 1,
    [Description("Nintendo GameCube")]
    NintendoGameCube = 2,
    [Description("Nintendo 64")]
    Nintendo64 = 3,
    [Description("Nintendo Game Boy")]
    NintendoGameBoy = 4,
    [Description("Nintendo Game Boy Advance")]
    NintendoGameBoyAdvance = 5,
    [Description("Super Nintendo (SNES)")]
    SuperNintendo_SNES = 6,
    [Description("Nintendo Entertainment System (NES)")]
    NintendoEntertainmentSystem_NES = 7,
    [Description("Nintendo DS")]
    NintendoDS = 8,
    [Description("Nintendo Wii")]
    NintendoWii = 9,
    [Description("Sony Playstation")]
    SonyPlaystation = 10,
    [Description("Sony Playstation 2")]
    SonyPlaystation2 = 11,
    [Description("Sony Playstation 3")]
    SonyPlaystation3 = 12,
    [Description("Sony Playstation Portable")]
    SonyPlaystationPortable = 13,
    [Description("Microsoft Xbox")]
    MicrosoftXbox = 14,
    [Description("Microsoft Xbox 360")]
    MicrosoftXbox360 = 15,
    [Description("Sega Dreamcast")]
    SegaDreamcast = 16,
    [Description("Sega Saturn")]
    SegaSaturn = 17,
    [Description("Sega Genesis")]
    SegaGenesis = 18,
    [Description("Sega Game Gear")]
    SegaGameGear = 20,
    [Description("Sega CD")]
    SegaCD = 21,
    [Description("Atari 2600")]
    Atari2600 = 22,
    [Description("Arcade")]
    Arcade = 23,
    [Description("Neo Geo")]
    NeoGeo = 24,
    [Description("3DO")]
    _3DO = 25,
    [Description("Atari 5200")]
    Atari5200 = 26,
    [Description("Atari 7800")]
    Atari7800 = 27,
    [Description("Atari Jaguar")]
    AtariJaguar = 28,
    [Description("Atari Jaguar CD")]
    AtariJaguarCD = 29,
    [Description("Atari XE")]
    AtariXE = 30,
    [Description("Colecovision")]
    Colecovision = 31,
    [Description("Intellivision")]
    Intellivision = 32,
    [Description("Sega 32X")]
    Sega32X = 33,
    [Description("TurboGrafx 16")]
    TurboGrafx16 = 34,
    [Description("Sega Master System")]
    SegaMasterSystem = 35,
    [Description("Sega Mega Drive")]
    SegaMegaDrive = 36,
    [Description("MacOS")]
    MacOS = 37,
    [Description("Nintendo Wii U")]
    NintendoWiiU = 38,
    [Description("Sony Playstation Vita")]
    SonyPlaystationVita = 39,
    [Description("Commodore 64")]
    Commodore64 = 40,
    [Description("Nintendo Game Boy Color")]
    NintendoGameBoyColor = 41,
    [Description("Amiga")]
    Amiga = 4911,
    [Description("Nintendo 3DS")]
    Nintendo3DS = 4912,
    [Description("Sinclair ZX Spectrum")]
    SinclairZXSpectrum = 4913,
    [Description("Amstrad CPC")]
    AmstradCPC = 4914,
    [Description("iOS")]
    iOS = 4915,
    [Description("Android")]
    Android = 4916,
    [Description("Philips CD-i")]
    PhilipsCDi = 4917,
    [Description("Nintendo Virtual Boy")]
    NintendoVirtualBoy = 4918,
    [Description("Sony Playstation 4")]
    SonyPlaystation4 = 4919,
    [Description("Microsoft Xbox One")]
    MicrosoftXboxOne = 4920,
    [Description("Ouya")]
    Ouya = 4921,
    [Description("Neo Geo Pocket")]
    NeoGeoPocket = 4922,
    [Description("Neo Geo Pocket Color")]
    NeoGeoPocketColor = 4923,
    [Description("Atari Lynx")]
    AtariLynx = 4924,
    [Description("WonderSwan")]
    WonderSwan = 4925,
    [Description("WonderSwan Color")]
    WonderSwanColor = 4926,
    [Description("Magnavox Odyssey 2")]
    MagnavoxOdyssey2 = 4927,
    [Description("Fairchild Channel F")]
    FairchildChannelF = 4928,
    [Description("MSX")]
    MSX = 4929,
    [Description("PC-FX")]
    PCFX = 4930,
    [Description("Sharp X68000")]
    SharpX68000 = 4931,
    [Description("FM Towns Marty")]
    FMTownsMarty = 4932,
    [Description("PC-88")]
    PC88 = 4933,
    [Description("PC-98")]
    PC98 = 4934,
    [Description("Nuon")]
    Nuon = 4935,
    [Description("Famicom Disk System")]
    FamicomDiskSystem = 4936,
    [Description("Atari ST")]
    AtariST = 4937,
    [Description("N-Gage")]
    NGage = 4938,
    [Description("Vectrex")]
    Vectrex = 4939,
    [Description("Game.com")]
    GameCom = 4940,
    [Description("TRS-80 Color Computer")]
    TRS80ColorComputer = 4941,
    [Description("Apple II")]
    AppleII = 4942,
    [Description("Atari 800")]
    Atari800 = 4943,
    [Description("Acorn Archimedes")]
    AcornArchimedes = 4944,
    [Description("Commodore VIC-20")]
    CommodoreVIC20 = 4945,
    [Description("Commodore 128")]
    Commodore128 = 4946,
    [Description("Amiga CD32")]
    AmigaCD32 = 4947,
    [Description("Mega Duck")]
    MegaDuck = 4948,
    [Description("SEGA SG-1000")]
    SEGASG1000 = 4949,
    [Description("Game & Watch")]
    Game_Watch = 4950,
    [Description("Handheld Electronic Games (LCD)")]
    HandheldElectronicGames_LCD = 4951,
    [Description("Dragon 32/64")]
    Dragon32_64 = 4952,
    [Description("Texas Instruments TI-99/4A")]
    TexasInstrumentsTI99_4A = 4953,
    [Description("Acorn Electron")]
    AcornElectron = 4954,
    [Description("TurboGrafx CD")]
    TurboGrafxCD = 4955,
    [Description("Neo Geo CD")]
    NeoGeoCD = 4956,
    [Description("Nintendo Pok\\u00e9mon Mini")]
    NintendoPokémonMini = 4957,
    [Description("Sega Pico")]
    SegaPico = 4958,
    [Description("Watara Supervision")]
    WataraSupervision = 4959,
    [Description("Tomy Tutor")]
    TomyTutor = 4960,
    [Description("Magnavox Odyssey 1")]
    MagnavoxOdyssey1 = 4961,
    [Description("Gakken Compact Vision")]
    GakkenCompactVision = 4962,
    [Description("Emerson Arcadia 2001")]
    EmersonArcadia2001 = 4963,
    [Description("Casio PV-1000")]
    CasioPV1000 = 4964,
    [Description("Epoch Cassette Vision")]
    EpochCassetteVision = 4965,
    [Description("Epoch Super Cassette Vision")]
    EpochSuperCassetteVision = 4966,
    [Description("RCA Studio II")]
    RCAStudioII = 4967,
    [Description("Bally Astrocade")]
    BallyAstrocade = 4968,
    [Description("APF MP-1000")]
    APFMP1000 = 4969,
    [Description("Coleco Telstar Arcade")]
    ColecoTelstarArcade = 4970,
    [Description("Nintendo Switch")]
    NintendoSwitch = 4971,
    [Description("Milton Bradley Microvision")]
    MiltonBradleyMicrovision = 4972,
    [Description("Entex Select-a-Game")]
    EntexSelectAGame = 4973,
    [Description("Entex Adventure Vision")]
    EntexAdventureVision = 4974,
    [Description("Pioneer LaserActive")]
    PioneerLaserActive = 4975,
    [Description("Action Max")]
    ActionMax = 4976,
    [Description("Sharp X1")]
    SharpX1 = 4977,
    [Description("Fujitsu FM-7")]
    FujitsuFM7 = 4978,
    [Description("SAM Coup\\u00e9")]
    SAMCoupé = 4979,
}

internal record Database
{
    public ushort? Code { get; set; }
    public string? Status { get; set; }
    [property: JsonPropertyName("last_edit_id")]
    public ulong? LastEditId { get; set; }
    public GamesData? Data { get; set; }
    public PlatformsBase? Platform { get; set; }
    [property: JsonPropertyName("remaining_monthly_allowance")]
    public ushort? RemainingMonthlyAllowance { get; set; }
    [property: JsonPropertyName("extra_allowance")]
    public ushort? ExtraAllowance { get; set; }
    [property: JsonPropertyName("allowance_refresh_timer")]
    public ulong? AllowanceRefreshTimer { get; set; }
    //public Pages? Pages { get; set; }
}

internal record GamesData
{
    public ulong? Count { get; set; }
    public List<Game>? Games { get; set; }
    public Include? Include { get; set; }
}

internal record Game
{
    public ulong? Id { get; set; }
    [property: JsonPropertyName("game_title")]
    public string? GameTitle { get; set; }
    [property: JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }
    public ushort? Platform { get; set; }
    [property: JsonPropertyName("region_id")]
    public ushort? RegionId { get; set; }
    [property: JsonPropertyName("country_id")]
    public ushort? CountryId { get; set; }
    public string? Overview { get; set; }
    public string? Youtube { get; set; }
    public ushort? Players { get; set; }
    public string? Coop { get; set; }
    public string? Rating { get; set; }
    public List<uint>? Developers { get; set; }
    public List<ushort>? Genres { get; set; }
    public List<uint>? Publishers { get; set; }
    public List<string>? Alternates { get; set; }
    public UId? Uids { get; set; }
    //public Hashes? Hashes { get; set; }
}

internal record UId
{
    public string? Uid { get; set; }
    [property: JsonPropertyName("games_uids_patterns_id")]
    public ushort? GamesUidsPatternsId { get; set; }
}

internal record Include
{
    public ArtBase? Boxart { get; set; }
}

internal record ArtBase
{
    [property: JsonPropertyName("base_url")]
    public BaseUrl? BaseUrl { get; set; }
    public Dictionary<ulong, ArtData>? Data { get; set; }
}

internal record BaseUrl
{
    public string? Original { get; set; }
    public string? Small { get; set; }
    public string? Thumb { get; set; }
    [property: JsonPropertyName("cropped_center_thumb")]
    public string? CroppedCenterThumb { get; set; }
    public string? Medium { get; set; }
    public string? Large { get; set; }
}

internal record ArtData
{
    public ulong? Id { get; set; }
    public string? Type { get; set; }
    public string? Side { get; set; }
    public string? Filename { get; set; }
    public string? Resolution { get; set; }
}

internal record PlatformsBase
{
    public Dictionary<ulong, PlatformsData>? Data { get; set; }
}

internal record PlatformsData
{
    public ushort? Id { get; set; }
    public string? Name { get; set; }
    public string? Alias { get; set; }
}
