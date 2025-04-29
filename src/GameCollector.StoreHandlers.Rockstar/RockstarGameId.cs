using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace GameCollector.StoreHandlers.Rockstar;

/// <summary>
/// Represents an id for games installed with Rockstar Games Launcher.
/// </summary>
[ValueObject<string>]
public readonly partial struct RockstarGameId : IAugmentWith<DefaultEqualityComparerAugment>
{
    /// <inheritdoc/>
    public static IEqualityComparer<string> InnerValueDefaultEqualityComparer { get; } = StringComparer.OrdinalIgnoreCase;
}

/// <inheritdoc/>
/// <param name="stringComparison"></param>
[PublicAPI]
public class RockstarGameIdComparer(StringComparison stringComparison) : IEqualityComparer<RockstarGameId>
{
    private static RockstarGameIdComparer? _default;

    /// <summary>
    /// Default equality comparer that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static RockstarGameIdComparer Default => _default ??= new();

    private readonly StringComparison _stringComparison = stringComparison;

    /// <summary>
    /// Default constructor that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public RockstarGameIdComparer() : this(StringComparison.OrdinalIgnoreCase) { }

    /// <inheritdoc/>
    public bool Equals(RockstarGameId x, RockstarGameId y) => string.Equals(x.Value, y.Value, _stringComparison);

    /// <inheritdoc/>
    public int GetHashCode(RockstarGameId obj) => obj.Value.GetHashCode(_stringComparison);
}
