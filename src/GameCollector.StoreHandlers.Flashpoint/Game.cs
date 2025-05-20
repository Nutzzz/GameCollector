namespace GameCollector.StoreHandlers.Flashpoint;

internal record Game
{
    public string? Id { get; init; }            // GUID
    //public string? ParentGameId { get; init; }  // (unused?) NULL
    public string? Title { get; init; }
    public string? AlternateTitles { get; init; } // (semicolon-separated)
    public string? Series { get; init; }
    public string? Developer { get; init; }
    public string? Publisher { get; init; }
    //public string? DateAdded { get; init; }     // yyyy-MM-dd'T'hh:mm:ss.SSS'Z' [sometimes SSSSS]
    //public string? DateModified { get; init; }  // yyyy-MM-dd'T'hh:mm:ss.SSS'Z' [sometimes SSSSS]
    //public bool? Broken { get; init; }          // (unused?) 0
    //public bool? Extreme { get; init; }         // (unused?) [NSFW] 0 [see tagFilters in preferences.json]
    public string? PlayMode { get; init; }      // (semicolon-separated) "Single Player", "Multiplayer", and/or "Cooperative"
    public string? Status { get; init; }        // (semicolon-separated) "Playable", "Hacked", and/or "Partial"
    public string? Notes { get; init; }
    //public string? Source { get; init; }
    public string? ApplicationPath { get; init; }
    public string? LaunchCommand { get; init; }
    public string? ReleaseDate { get; init; }   // yyyy or yyyy-MM or yyyy-MM-dd
    public string? Version { get; init; }
    public string? OriginalDescription { get; init; }
    public string? Language { get; init; }      // (semicolon-separated) "ar", "be", "bs", "cs", "cy", "da", "de", etc.
    public string? Library { get; init; }       // "arcade" or "theatre"
    //public string? OrderTitle { get; init; }    // (unused?)
    public ulong? ActiveDataId { get; init; }   // NULL or number
    //public string? ActiveDataOnDisk { get; init; } // (unused?) 0
    public string? TagsStr { get; init; }       // (semicolon-separated) "Arcade", "Comedy", "Informative", "Pixel", "Shooter", "Toy", etc.
    public string? PlatformsStr { get; init; }
    //public string? PlatformId { get; init; }    // sometimes a number, but most often repetitive of PlatformName
    public string? LastPlayed { get; init; }    // NULL or yyyy-MM-dd'T'hh:mm:ss.SSS'Z'
    public uint? Playtime { get; init; }
    public ushort? PlayCounter { get; init; }
    public string? ArchiveState { get; init; }  // 1 or 2 [though almost all are 2] // TODO: what does it mean?
    //public string? ActiveGameConfigId { get; init; } // (unused?) NULL
    //public string? ActiveGameConfigOwner { get; init; } // (unused?) NULL
    public string? PlatformName { get; init; }  // "Flash", "HTML5", "Java", "Shockwave", "Silverlight", etc.
    public string? RuffleSupport { get; init; } // "" or "standalone" [almost all ""]
}
