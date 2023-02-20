﻿using GameCollector.RegistryUtils;
using TestUtils;

namespace GameCollector.StoreHandlers.GOG.Tests;

public partial class GOGTests
{
    [Theory, AutoData]
    public void Test_ShouldError_InvalidGameId(InMemoryRegistry registry, string keyName, string gameId)
    {
        var (handler, gogKey) = SetupHandler(registry);

        var invalidKey = gogKey.AddSubKey(keyName);
        invalidKey.AddValue("gameId", gameId);

        var error = handler.ShouldOnlyBeOneError();
        error.Should().Be($"The value \"gameID\" of {invalidKey.GetName()} is not a number: \"{gameId}\"");
    }
}
