using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameCollector.EmuHandlers.MAME.Properties;
using NexusMods.Paths;

namespace GameCollector.EmuHandlers.MAME;

// Originally based on https://github.com/mika76/mamesaver
// Copyright (c) 2007 Contributors

public partial class CategoryParser
{
    private const string CategorySection = "Category";
    private const string VerAddedSection = "VerAdded";

    private static readonly Dictionary<string, Category> Categories = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Versions = new(StringComparer.Ordinal);

    public static string GetVersion(AbsolutePath exePath, string gameName, IFileSystem fileSystem)
    {
        if (Versions.Count == 0)
            LoadCatFile(exePath, fileSystem);

        return Versions.TryGetValue(gameName, out var value) ? value : "";
    }

    internal static Category GetCategory(AbsolutePath exePath, string gameName, IFileSystem fileSystem)
    {
        if (Categories.Count == 0)
            LoadCatFile(exePath, fileSystem);

        return Categories.TryGetValue(gameName, out var value) ? value : new Category("Unknown", "", "", mature: false);
    }

    private static Version? GetCatVersion(string catText)
    {
        var lines = catText.Split(Environment.NewLine);
        foreach (var line in lines)
        {
            if (line.StartsWith(";; catver.ini", StringComparison.Ordinal) &&
                line.Length > 14)
            {
                var version = line[14..];
                if (version.Contains(' ', StringComparison.Ordinal))
                    version = version[..version.IndexOf(' ', StringComparison.Ordinal)];
                if (Version.TryParse(version, out var outVersion))
                    return outVersion;
            }
        }

        return new();
    }

    private static void LoadCatFile(AbsolutePath exePath, IFileSystem fileSystem)
    {
        var mamePath = fileSystem.FromUnsanitizedFullPath(exePath.Directory);
        if (string.IsNullOrEmpty(mamePath.GetFullPath()))
            return;

        var inCategorySection = false;
        var inVerAddedSection = false;
        var catText = "";
        var catVersionIncluded = GetCatVersion(Resources.catver);
        Version? catVersionUser = new();
        var catPathUser = fileSystem.GetKnownPath(KnownPath.CurrentDirectory).Combine("catver.ini");
        if (!fileSystem.FileExists(catPathUser))
        {
            catPathUser = mamePath.Combine("catver.ini");
            if (!fileSystem.FileExists(catPathUser))
                catPathUser = new();
        }
        if (!string.IsNullOrEmpty(catPathUser.GetFullPath()))
        {
            catText = File.ReadAllText(catPathUser.GetFullPath());
            catVersionUser = GetCatVersion(catText);
        }
        if (catVersionUser < catVersionIncluded)
            catText = Resources.catver;

        var lines = catText.Split(Environment.NewLine);
        foreach (var line in lines)
        {
            if (IsSectionLine(line))
            {
                if (IsVerAddedSection(line))  // Scan for VerAdded section
                    inVerAddedSection = true;
                else if (IsCategorySection(line))  // Scan for Category section
                    inCategorySection = true;
            }
            else if (IsGameLine(line))
            {
                if (inVerAddedSection)
                    RegisterGameVer(line);
                else if (inCategorySection)
                    RegisterGameCat(line);
            }
        }
    }

    private static void RegisterGameCat(string line)
    {
        var mature = false;
        if (line.EndsWith("* Mature *", StringComparison.Ordinal))
            line = line[..line.LastIndexOf("* Mature *", StringComparison.Ordinal)];

        var match = DetailRegex().Match(line);
        var detail = match.Groups[2].Value.Split('/');

        var category1 = detail[0].Trim();
        var category2 = detail.Length > 1 ? detail[1].Trim() : "";
        var category3 = detail.Length > 2 ? detail[2].Trim() : "";

        Categories[match.Groups[1].Value] = new(category1, category2, category3, mature);
    }

    private static void RegisterGameVer(string line)
    {
        var match = DetailRegex().Match(line);
        var verAdded = match.Groups[2].Value.Trim();

        Versions[match.Groups[1].Value] = new(verAdded);
    }

    private static bool IsGameLine(string line) => DetailRegex().IsMatch(line);
    private static bool IsSectionLine(string line) => SectionRegex().IsMatch(line);

    private static bool IsCategorySection(string line)
    {
        //if (!IsSectionLine(line)) return false;
        return string.Equals(SectionRegex().Match(line).Groups[1].Value, CategorySection, StringComparison.Ordinal);
    }

    private static bool IsVerAddedSection(string line)
    {
        //if (!IsSectionLine(line)) return false;
        return string.Equals(SectionRegex().Match(line).Groups[1].Value, VerAddedSection, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\s*\[(.*)\]\s*")]
    private static partial Regex SectionRegex();
    [GeneratedRegex(@"([^=]+)=(.+)")]
    private static partial Regex DetailRegex();
}
