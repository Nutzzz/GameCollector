using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace GameCollector.StoreHandlers.Legacy;

/// <summary>
/// Represents an id for games installed with Legacy Games Launcher.
/// </summary>
[ValueObject<string>]
public readonly partial struct LegacyGameId : IAugmentWith<DefaultEqualityComparerAugment>
{
    /// <inheritdoc/>
    public static IEqualityComparer<string> InnerValueDefaultEqualityComparer { get; } = StringComparer.OrdinalIgnoreCase;
}

/// <inheritdoc/>
[PublicAPI]
public class LegacyGameIdComparer : IEqualityComparer<LegacyGameId>
{
    private static LegacyGameIdComparer? _default;

    /// <summary>
    /// Default equality comparer that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static LegacyGameIdComparer Default => _default ??= new();

    private readonly StringComparison _stringComparison;

    /// <summary>
    /// Default constructor that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public LegacyGameIdComparer() : this(StringComparison.OrdinalIgnoreCase) { }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="stringComparison"></param>
    public LegacyGameIdComparer(StringComparison stringComparison)
    {
        _stringComparison = stringComparison;
    }

    /// <inheritdoc/>
    public bool Equals(LegacyGameId x, LegacyGameId y) => string.Equals(x.Value, y.Value, _stringComparison);

    /// <inheritdoc/>
    public int GetHashCode(LegacyGameId obj) => obj.Value.GetHashCode(_stringComparison);
}
