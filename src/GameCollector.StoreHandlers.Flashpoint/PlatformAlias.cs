namespace GameCollector.StoreHandlers.Flashpoint;

internal record PlatformAlias
{
    public int? Id { get; init; }

    public int? PlatformId { get; init; }

    public string? Name { get; init; }
}
