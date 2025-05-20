namespace GameCollector.PkgHandlers.Scoop;

internal record Info(
    string? Name,       // [not in cat]
    string? Description,
    string? Version,
    string? Bucket,     // [not in cat]
    string? Website,    // cat "homepage"
    string? License,    // cat "license" or "license">"identifier"
    string? UpdatedAt,  // [not in cat]
    string? UpdatedBy,  // [not in cat]
    string? Installed,  // [not in cat]
    string? Binaries,   // cat "bin"
    string? Shortcuts,  // cat might have more
    string? Notes
);

internal record Search(
    string? Name,
    string? Version,
    string? Source,
    string? Binaries
);
