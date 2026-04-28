using JetBrains.Annotations;
using System.Collections.Generic;

namespace GameCollector.StoreHandlers.EGS;

[UsedImplicitly]
internal record ManifestFile(
    string? CatalogItemId,
    string? DisplayName,
    string? InstallLocation,
    string? ManifestHash,
    string? MainGameCatalogItemId,
    int? FormatVersion,
    string? LaunchExecutable,
    string? InstallationGuid,
    string? AppName,
    string? MainGameAppName
);
