﻿using GameCollector.RegistryUtils;
using TestUtils;

namespace GameCollector.StoreHandlers.GOG.Tests;

public partial class GOGTests
{
    [Theory, AutoData]
    public void Test_ShouldError_MissingGOGKey(InMemoryRegistry registry)
    {
        var handler = new GOGHandler(registry);

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"Unable to open HKEY_LOCAL_MACHINE\\{GOGHandler.GOGRegKey}");
    }
}
