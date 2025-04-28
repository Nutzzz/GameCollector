using System;
using System.Collections.Generic;
using GameFinder.Common;
using HtmlAgilityPack;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.BigFish;

/// <summary>
/// Represents a game installed with Big Fish Game Manager.
/// </summary>
/// <param name="ProductId"></param>
/// <param name="Name"></param>
/// <param name="Path"></param>
/// <param name="ExecutablePath"></param>
/// <param name="Icon"></param>
/// <param name="Uninstall"></param>
/// <param name="LastActionTime"></param>
/// <param name="PlayCount"></param>
/// <param name="IsInstalled"></param>
/// <param name="IsExpired"></param>
/// <param name="ImageUrl"></param>
[PublicAPI]
public record BigFishGame(BigFishGameId ProductId,
                         string Name,
                         AbsolutePath Path,
                         AbsolutePath ExecutablePath = new(),
                         AbsolutePath Icon = new(),
                         AbsolutePath Uninstall = new(),
                         DateTime? LastActionTime = null,
                         uint? PlayCount = null,
                         bool IsInstalled = true,
                         bool IsExpired = false,
                         string? ImageUrl = "") :
    GameData(Handler: Handler.StoreHandler_BigFish,
             GameId: ProductId.ToString(),
             GameName: Name,
             GamePath: Path,
             Launch: ExecutablePath,
             Icon: Icon,
             Uninstall: Uninstall,
             LastRunDate: LastActionTime,
             NumRuns: PlayCount ?? 0,
             IsInstalled: IsInstalled,
             Problems: IsExpired ? new List<Problem>() { Problem.ExpiredTrial } : null,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["ImageUrl"] = [ImageUrl ?? "",],
             })
{
    public static string GetImageUrl(string id)
    {
        var url = $"{BigFishHandler.BigFishUrl}{id}/";

        HtmlWeb web = new() { UseCookies = true, };
        var doc = web.Load(url);
        doc.OptionUseIdAttribute = true;
        var node = doc.DocumentNode.SelectSingleNode("//div[@class='rr-game-image']");
        if (node is not null && node.HasChildNodes)
        {
            foreach (var child in node.ChildNodes)
            {
                foreach (var attr in child.Attributes)
                {
                    if (attr.Name.Equals("src", StringComparison.OrdinalIgnoreCase))
                    {
                        return attr.Value;
                    }
                }
            }
        }
        return "";
    }
}
