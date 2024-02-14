using TransparentValueObjects;

namespace GameCollector.StoreHandlers.GOG;

/// <summary>
/// Represents an id for games installed with GOG Galaxy.
/// </summary>
[ValueObject<long>]
public readonly partial struct GOGGameId { }
