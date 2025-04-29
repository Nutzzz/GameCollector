using TransparentValueObjects;

namespace GameCollector.DataHandlers.TheGamesDB;

/// <summary>
/// Represents an id for game data from TheGamesDB.net.
/// </summary>
[ValueObject<ulong>]
public readonly partial struct TheGamesDBGameId { }
