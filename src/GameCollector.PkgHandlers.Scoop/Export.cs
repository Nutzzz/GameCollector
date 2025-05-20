using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace GameCollector.PkgHandlers.Scoop;

[UsedImplicitly]
internal record Export(
    ExportConfig? Config,
    List<ExportBuckets>? Buckets,
    List<ExportApps>? Apps
);

[UsedImplicitly]
internal record ExportConfig(
    [property: JsonPropertyName("use_external_7zip")]   string? UseExternal7zip,    // $true|$false
    [property: JsonPropertyName("use_lessmsi")]         string? UseLessmsi,         // $true|$false
    [property: JsonPropertyName("use_sqlite_cache")]    string? UseSqliteCache,     // $true|$false
    [property: JsonPropertyName("no_junction")]         string? NoJunction,         // $true|$false
    [property: JsonPropertyName("scoop_repo")]          string? ScoopRepo,          // <string url>
    [property: JsonPropertyName("scoop_branch")]        string? ScoopBranch,        // master|develop
    string? Proxy,              // <string, [username:password@]host:port>
    [property: JsonPropertyName("autostash_on_conflict")] string? AutostashOnConflict, // $true|$false
    [property: JsonPropertyName("default_architecture")] string? DefaultArchitecture, // 64bit|32bit|arm64
    string? Debug,              // $true|$false
    [property: JsonPropertyName("force_update")]        string? ForceUpdate,        // $true|$false
    [property: JsonPropertyName("show_update_log")]     string? ShowUpdateLog,      // $true|$false
    [property: JsonPropertyName("show_manifest")]       string? ShowManifest,       // $true|$false
    string? Shim,               // kiennq|scoopcs|71
    [property: JsonPropertyName("root_path")]           string? RootPath,           // <string path, default: $Env:UserProfile\scoop>
    [property: JsonPropertyName("global_path")]         string? GlobalPath,         // <string path, default: $Env:ProgramData\scoop>
    [property: JsonPropertyName("cache_path")]          string? CachePath,          // <string path, default: <root_path>\cache>
    [property: JsonPropertyName("gh_token")]            string? GhToken,            // <string>
    [property: JsonPropertyName("virustotal_api_key")]  string? VirtusotalApiKey,   // <string>
    [property: JsonPropertyName("cat_style")]           string? CatStyle,           // <string>
    [property: JsonPropertyName("ignore_running_processes")] string? IgnoreRunningProcesses, // $true|$false
    [property: JsonPropertyName("private_hosts")]       List<string>? PrivateHosts, // <array>
    [property: JsonPropertyName("hold_update_until")]   string? HoldUpdateUntil,    // <DateTime::Parse(), e.g. YYYY-MM-DD>
    [property: JsonPropertyName("update_nightly")]      string? UpdateNightly,      // $true|$false
    [property: JsonPropertyName("use_isolated_path")]   string? UseIsolatedPath,    // $true|$false|<string>
    [property: JsonPropertyName("aria2-enabled")]       string? Aria2Enabled,       // $true|$false
    [property: JsonPropertyName("aria2-warning-enabled")] string? Aria2WarningEnabled, // $true|$false
    [property: JsonPropertyName("aria2-retry-wait")]    string? Aria2RetryWait,     // <int default: 2>
    [property: JsonPropertyName("aria2-split")]         string? Aria2Split,         // <int default: 5>
    [property: JsonPropertyName("aria2-max-connection-per-server")] string Aria2MaxConnectionPerServer, // <int default: 5>
    [property: JsonPropertyName("aria2-min-split-size")] string? Aria2MinSplitSize, // <string default: 5M>
    [property: JsonPropertyName("aria2-options")]       List<string>? Aria2Options  // <array>
);

[UsedImplicitly]
internal record ExportBuckets(
    string? Name,
    string? Source,
    string? Updated,
    List<string>? Manifests
);

[UsedImplicitly]
internal record ExportApps(
    string? Info, // Install Info, none or more of "<name> missing", "Install failed", "Held package", "Manifest removed"
    string? Name,
    string? Source,
    string? Updated,
    string? Version
);
