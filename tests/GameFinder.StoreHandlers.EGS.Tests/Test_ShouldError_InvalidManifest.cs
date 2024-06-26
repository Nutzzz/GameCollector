using GameFinder.RegistryUtils;
using NexusMods.Paths;
using NexusMods.Paths.TestingHelpers;
using TestUtils;

namespace GameFinder.StoreHandlers.EGS.Tests;

public partial class EGSTests
{
    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldError_InvalidManifest_Exception(InMemoryFileSystem fs,
        InMemoryRegistry registry, string manifestItemName)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var randomBytes = new byte[128];
        Random.Shared.NextBytes(randomBytes);

        var manifestItem = manifestDir.Combine($"{manifestItemName}.item");
        fs.AddFile(manifestItem, randomBytes);

        var error = handler.ShouldOnlyBeOneError();
        error.ToString().Should().StartWith($"Unable to deserialize file {manifestItem}:\n");
    }

    [Theory(Skip = "Fix me"), AutoFileSystem]
    public void Test_ShouldError_InvalidManifest_Null(InMemoryFileSystem fs,
        InMemoryRegistry registry, string manifestItemName)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var manifestItem = manifestDir.Combine($"{manifestItemName}.item");
        fs.AddFile(manifestItem, "null");

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"Unable to deserialize file {manifestItem}");
    }
}
