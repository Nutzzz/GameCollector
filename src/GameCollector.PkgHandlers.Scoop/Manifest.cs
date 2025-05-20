using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameCollector.PkgHandlers.Scoop;

internal record Manifest(
    string? Version,
    string? Description,
    string? Homepage,
    License? License,
    string[]? Notes,
    string? Url,
    string? Hash,
    Suggest? Suggest,
    Architecture? Architecture,
    string[]? PreInstall,
    string? ExtractDir,
    Installer? Installer,
    string[]? PostInstall,
    Uninstaller? Uninstaller,
    string[]? Bin,
    List<string[]>? Shortcuts,
    string[]? Persist,      // files or dirs
    string[]? PreUninstall,
    Checkver? Checkver,     // string or structure
    Autoupdate? Autoupdate  // string or structure
);

internal record License(
    string? Tag, // if no children
    string? Identifier,
    string? Url
);

internal record Suggest(
    //string? Encoders
);

internal record Architecture(
    [property: JsonPropertyName("64bit")]
    x64Bit? x64Bit,
    [property: JsonPropertyName("32bit")]
    x32Bit? x32Bit,
    Arm64? Arm64
);

internal record Installer(
    string[]? Script
);

internal record Uninstaller(
    string[]? Script
);

internal record Checkver(
    string? Tag, // if no children
    string? Url,
    string? Useragent,
    string[]? Script,
    string? Jsonpath,
    string? Regex
);

internal record Autoupdate(
    string? Url,
    Architecture? Architecture,
    Hash? Hash
);

internal record x64Bit(
    string? Url,
    Installer? Installer,
    Hash? Hash
);

internal record x32Bit(
    string? Url,
    Installer? Installer,
    Hash? Hash
);

internal record Arm64(
    string? Url,
    Installer? Installer,
    Hash? Hash
);

internal record Hash(
    string? Tag, // if no children
    string? Url,
    string? Regex
);
