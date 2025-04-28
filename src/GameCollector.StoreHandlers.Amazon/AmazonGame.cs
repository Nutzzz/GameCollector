using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameCollector.StoreHandlers.Amazon;

/// <summary>
/// Represents a game installed with Amazon Games.
/// </summary>
/// <param name="ProductId"></param>
/// <param name="ProductTitle"></param>
/// <param name="InstallDirectory"></param>
/// <param name="Command"></param>
/// <param name="LaunchUrl"></param>
/// <param name="Icon"></param>
/// <param name="Uninstall"></param>
/// <param name="IsInstalled"></param>
/// <param name="InstallDate"></param>
/// <param name="ReleaseDate"></param>
/// <param name="ProductDescription"></param>
/// <param name="ProductIconUrl"></param>
/// <param name="ProductLogoUrl"></param>
/// <param name="Screenshots"></param>
/// <param name="Videos"></param>
/// <param name="Developers"></param>
/// <param name="ProductPublisher"></param>
/// <param name="EsrbRating"></param>
/// <param name="WebInfo"></param>
/// <param name="WebSupport"></param>
/// <param name="GameModes"></param>
/// <param name="Genres"></param>
[PublicAPI]
public record AmazonGame(AmazonGameId ProductId,
                         string? ProductTitle,
                         AbsolutePath InstallDirectory,
                         AbsolutePath Command = new(),
                         string LaunchUrl = "",
                         AbsolutePath Icon = new(),
                         AbsolutePath Uninstall = new(),
                         bool IsInstalled = true,
                         DateTime? InstallDate = null,
                         DateTime? ReleaseDate = null,
                         string? ProductDescription = null,
                         string? ProductIconUrl = null,
                         string? ProductLogoUrl = null,
                         string? Screenshots = "",
                         string? Videos = "",
                         string? Developers = "",
                         string? ProductPublisher = null,
                         EsrbRating EsrbRating = (EsrbRating)(-1),
                         string? WebInfo = "",
                         string? WebSupport = "",
                         string? GameModes = "",
                         string? Genres = "") :
    GameData(Handler: Handler.StoreHandler_Amazon,
             GameId: ProductId.ToString(),
             GameName: ProductTitle ?? "",
             GamePath: InstallDirectory,
             Launch: Command,
             LaunchUrl: LaunchUrl,
             Icon: Icon == default ? Command : Icon,
             Uninstall: Uninstall,
             IsInstalled: IsInstalled,
             InstallDate: InstallDate,
             Metadata: new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
             {
                 ["ReleaseDate"] = [ReleaseDate is null ? "" : ((DateTime)ReleaseDate).ToString(CultureInfo.InvariantCulture),],
                 ["Description"] = [ProductDescription ?? "",],
                 ["ImageUrl"] = [ProductIconUrl ?? "",],
                 ["LogoUrl"] = [ProductLogoUrl ?? "",],
                 ["Screenshots"] = string.IsNullOrEmpty(Screenshots) ? [] : GetJsonArray(@Screenshots ?? ""),
                 ["Videos"] = string.IsNullOrEmpty(Videos) ? [] : GetJsonArray(@Videos ?? ""),
                 ["WebInfo"] = string.IsNullOrEmpty(WebInfo) ? [] : GetJsonArray(@WebInfo ?? ""),
                 ["WebSupport"] = string.IsNullOrEmpty(WebSupport) ? [] : GetJsonArray(@WebSupport ?? ""),
                 ["Developers"] = string.IsNullOrEmpty(Developers) ? [] : GetJsonArray(@Developers ?? ""),
                 ["Publishers"] = [ProductPublisher ?? "",],
                 ["AgeRating"] = [EsrbRating == (EsrbRating)(-1) ? "" : EsrbRating.ToString(),],
                 ["Players"] = string.IsNullOrEmpty(GameModes) ? [] : GetJsonArray(@GameModes ?? ""),
                 ["Genres"] = string.IsNullOrEmpty(Genres) ? [] : GetJsonArray(@Genres ?? ""),
             })
{
    internal static List<string> GetJsonArray(string json)
    {
        List<string> list = [];
        using var doc = JsonDocument.Parse(json, new() { AllowTrailingCommas = true, });
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            list.Add(element.GetString() ?? "");
        }
        return list;
    }
}
