﻿using System.IO.Abstractions.TestingHelpers;
using GameCollector.RegistryUtils;
using TestUtils;

namespace GameCollector.StoreHandlers.EGS.Tests;

public partial class EGSTests
{
    [Theory, AutoData]
    public void Test_ShouldError_NoManifests(MockFileSystem fs, InMemoryRegistry registry)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"The manifest directory {manifestDir} does not contain any .item files");
    }
}
