namespace GameCollector.StoreHandlers.Flashpoint;

internal record Status
{
    internal bool? Hacked { get; set; }
    internal bool? Partial { get; set; }
    internal bool? Playable { get; set; }
}
