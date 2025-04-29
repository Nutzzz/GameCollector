using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace GameCollector.StoreHandlers.Ubisoft;

/// <summary>
/// Represents an id for games installed with Ubisoft Connect.
/// </summary>
[ValueObject<string>]
public readonly partial struct UbisoftGameId : IAugmentWith<DefaultEqualityComparerAugment>
{
    /// <inheritdoc/>
    public static IEqualityComparer<string> InnerValueDefaultEqualityComparer { get; } = StringComparer.OrdinalIgnoreCase;
}

/// <inheritdoc/>
/// <param name="stringComparison"></param>
[PublicAPI]
public class UbisoftGameIdComparer(StringComparison stringComparison) : IEqualityComparer<UbisoftGameId>
{
    private static UbisoftGameIdComparer? _default;

    /// <summary>
    /// Default equality comparer that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static UbisoftGameIdComparer Default => _default ??= new();

    private readonly StringComparison _stringComparison = stringComparison;

    /// <summary>
    /// Default constructor that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public UbisoftGameIdComparer() : this(StringComparison.OrdinalIgnoreCase) { }

    /// <inheritdoc/>
    public bool Equals(UbisoftGameId x, UbisoftGameId y) => string.Equals(x.Value, y.Value, _stringComparison);

    /// <inheritdoc/>
    public int GetHashCode(UbisoftGameId obj) => obj.Value.GetHashCode(_stringComparison);
}
