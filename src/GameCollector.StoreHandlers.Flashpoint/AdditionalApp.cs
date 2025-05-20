namespace GameCollector.StoreHandlers.Flashpoint;

internal record AdditionalApp
{
    public string? Id { get; init; }            // GUID

    public string? ApplicationPath { get; init; }

    public bool? AutoRunBefore { get; init; }

    public string? LaunchCommand { get; init; }

    public string? Name { get; init; }

    public bool? WaitForExit { get; init; }

    public string? ParentGameId { get; init; }  // GUID
}
