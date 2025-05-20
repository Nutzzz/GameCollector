namespace GameCollector.StoreHandlers.Flashpoint;

internal record DataPacks
{
    public ulong? Id { get; init; }

    public string? GameId { get; init; }        // GUID

    //public string? Title { get; init; }         // (unused) "Data Pack"

    // dateAdded is also present in "game" table with a (slightly?) different time
    //public string? DateAdded { get; init; }     // YYYY-MM-DD'T'hh:mm:ss.fff'Z' [sometimes fffff]

    public string? Sha256 { get; init; }

    //public string? Crc32 { get; init; }         // (unused) 0

    public bool? PresentOnDisk { get; init; }   // 0 or 1

    public string? Path { get; init; }          // GUID

    public ulong? Size { get; init; }

    public string? Parameters { get; init; }    // NULL or params

    // applicationPath and launchCommand are also present in "game" table, but are (always?) blank when a "game_data" entry with the same GameId is also present
    public string? ApplicationPath { get; init; } //

    public string? LaunchCommand { get; init; }
}
