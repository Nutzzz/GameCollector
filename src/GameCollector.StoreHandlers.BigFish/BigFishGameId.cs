using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace GameCollector.StoreHandlers.BigFish;

/// <summary>
/// Represents an id for games installed with Big Fish Game Manager.
/// </summary>
[ValueObject<string>]
public readonly partial struct BigFishGameId : IAugmentWith<DefaultEqualityComparerAugment>
{
    /// <inheritdoc/>
    public static IEqualityComparer<string> InnerValueDefaultEqualityComparer { get; } = StringComparer.OrdinalIgnoreCase;
}

/// <inheritdoc/>
/// <param name="stringComparison"></param>
[PublicAPI]
public class BigFishGameIdComparer(StringComparison stringComparison) : IEqualityComparer<BigFishGameId>
{
    private static BigFishGameIdComparer? _default;

    /// <summary>
    /// Default equality comparer that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static BigFishGameIdComparer Default => _default ??= new();

    private readonly StringComparison _stringComparison = stringComparison;

    /// <summary>
    /// Default constructor that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public BigFishGameIdComparer() : this(StringComparison.OrdinalIgnoreCase) { }

    /// <inheritdoc/>
    public bool Equals(BigFishGameId x, BigFishGameId y) => string.Equals(x.Value, y.Value, _stringComparison);

    /// <inheritdoc/>
    public int GetHashCode(BigFishGameId obj) => obj.Value.GetHashCode(_stringComparison);
}
