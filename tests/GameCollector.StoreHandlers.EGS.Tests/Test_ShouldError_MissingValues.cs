﻿using System.IO.Abstractions.TestingHelpers;
using GameCollector.RegistryUtils;
using GameCollector.StoreHandlers.EGS.Tests.AutoData;
using TestUtils;

namespace GameCollector.StoreHandlers.EGS.Tests;

public partial class EGSTests
{
    [Theory, EGSAutoData]
    public void Test_ShouldError_Missing_CatalogItemId(MockFileSystem fs,
        InMemoryRegistry registry, string manifestItemName)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var manifest = fs.Path.Combine(manifestDir, $"{manifestItemName}.item");
        fs.AddFile(manifest, "{}");

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"Manifest {manifest} does not have a value \"CatalogItemId\"");
    }

    [Theory, EGSAutoData]
    public void Test_ShouldError_Missing_DisplayName(MockFileSystem fs,
        InMemoryRegistry registry, string manifestItemName, string value)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var manifest = fs.Path.Combine(manifestDir, $"{manifestItemName}.item");
        fs.AddFile(manifest, $@"{{ ""CatalogItemId"": ""{value}"" }}");

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"Manifest {manifest} does not have a value \"DisplayName\"");
    }

    [Theory, EGSAutoData]
    public void Test_ShouldError_Missing_InstallLocation(MockFileSystem fs,
        InMemoryRegistry registry, string manifestItemName, string value)
    {
        var (handler, manifestDir) = SetupHandler(fs, registry);

        var manifest = fs.Path.Combine(manifestDir, $"{manifestItemName}.item");
        fs.AddFile(manifest,$@"{{ ""CatalogItemId"": ""{value}"", ""DisplayName"": ""{value}"" }}");

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"Manifest {manifest} does not have a value \"InstallLocation\"");
    }
}
