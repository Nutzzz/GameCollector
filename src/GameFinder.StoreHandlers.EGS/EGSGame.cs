using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameFinder.StoreHandlers.EGS;

/// <summary>
/// Represents a game installed with the Epic Games Store.
/// </summary>
/// <param name="EGSGameId"></param>
/// <param name="DisplayName"></param>
/// <param name="InstallLocation"></param>
[PublicAPI]
public record EGSGame(EGSGameId EGSGameId, string DisplayName, AbsolutePath InstallLocation);
