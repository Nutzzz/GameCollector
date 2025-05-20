using System.Collections.Generic;

namespace GameCollector.StoreHandlers.Flashpoint;

internal record Preferences(
    List<TagFilter>? TagFilters
);

internal record TagFilter(
    string? Name,
    string? Description,
    bool? Enabled,
    List<string>? Tags,
    //List<Category> Catagories,      // (unused)
    //List<ChildFilter> ChildFilters, // (unused)
    bool? Extreme
    //string? IconBase64              // (unused)
);
