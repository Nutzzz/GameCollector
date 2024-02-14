using FluentResults.Extensions.FluentAssertions;
using GameCollector.StoreHandlers.Steam.Models;
using GameCollector.StoreHandlers.Steam.Models.ValueTypes;
using GameCollector.StoreHandlers.Steam.Services;
using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;

namespace GameFinder.StoreHandlers.Steam.Tests.Services;

public class AppManifestParserTests
{
    [Theory, AutoFileSystem]
    public void Test_Success_OnlyRequired(AbsolutePath manifestFilePath)
    {
        var expected = new AppManifest
        {
            ManifestPath = manifestFilePath,
            AppId = AppId.From(262060),
            Name = "Darkest Dungeon",
            StateFlags = StateFlags.FullyInstalled,
            InstallationDirectory = manifestFilePath.Parent.Combine("common").Combine("DarkestDungeon"),
        };

        var writeResult = AppManifestWriter.Write(expected, manifestFilePath);
        writeResult.Should().BeSuccess();

        var result = AppManifestParser.ParseManifestFile(manifestFilePath);
        result.Should().BeSuccess().And.HaveValue(expected);
    }

    [Theory, AutoFileSystem]
    public void Test_Success_Everything(AbsolutePath manifestFilePath)
    {
        var expected = ArrangeHelper.CreateAppManifest(manifestFilePath);

        var writeResult = AppManifestWriter.Write(expected, manifestFilePath);
        writeResult.Should().BeSuccess();

        var result = AppManifestParser.ParseManifestFile(manifestFilePath);
        result.Should().BeSuccess().And.HaveValue(expected);
    }
}
