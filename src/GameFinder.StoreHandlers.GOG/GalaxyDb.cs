using GameCollector.SQLiteUtils;
using JetBrains.Annotations;

namespace GameCollector.StoreHandlers.GOG;

[UsedImplicitly]
internal record LimitedDetails
{
    public string? Links { get; init; }
    public string? Images { get; init; }
    public string? ProductId { get; init; }
    public string? Title { get; init; }
}

[UsedImplicitly]
internal record LinksJson(
    LinkUrl? BoxArtImage,
    LinkUrl? Forum,
    LinkUrl? Icon,
    LinkUrl? IconSquare,
    LinkUrl? Logo,
    LinkUrl? Store,
    LinkUrl? Support
);

[UsedImplicitly]
internal record LinkUrl(
    string? Href
);

[UsedImplicitly]
internal record ImagesJson(
    string? Logo2x
);

[UsedImplicitly]
internal record Builds
{
    public string? ProductId { get; init; }
}

[UsedImplicitly]
internal record ProductsToReleaseKeys
{
    public string? GogId { get; init; }
    public string? ReleaseKey { get; init; }
}

[UsedImplicitly]
internal record GamePieces
{
    public string? ReleaseKey { get; init; }
    public string? Value { get; init; }
}

internal record ValueJson(
    string? ParentGrk,
    string? Rating
);

[UsedImplicitly]
internal record InstalledBaseProducts
{
    public string? ProductId { get; init; }
    public string? InstallationPath { get; init; }
    public ulong? BuildId { get; init; }
    public string? InstallationDate { get; init; }
}

[UsedImplicitly]
internal record PlayTasks
{
    public string? GameReleaseKey { get; init; }
    [property: SqlColName("id")]
    public string? Id { get; init; }
}

[UsedImplicitly]
internal record PlayTaskLaunchParameters
{
    public string? PlayTaskId { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CommandLineArgs { get; init; }
}

[UsedImplicitly]
internal record UserReleaseProperties
{
    public string? ReleaseKey { get; init; }
    public string? IsHidden { get; init; }
}

[UsedImplicitly]
internal record UserReleaseTags
{
    public string? ReleaseKey { get; init; }
    public string? Tag { get; init; }
}

[UsedImplicitly]
internal record LastPlayedDates
{
    public string? GameReleaseKey { get; init; }
    public string? LastPlayedDate { get; init; }
}

[UsedImplicitly]
internal record Details
{
    public string? LimitedDetailsId { get; init; }
    public string? ReleaseDate { get; init; }
}
