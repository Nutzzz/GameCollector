using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace GameCollector.EmuHandlers.MAME;

/// <summary>
/// Represents an id for ROMs for MAME.
/// </summary>
[ValueObject<string>]
public readonly partial struct MAMEGameId : IAugmentWith<DefaultEqualityComparerAugment>
{
    /// <inheritdoc/>
    public static IEqualityComparer<string> InnerValueDefaultEqualityComparer { get; } = StringComparer.OrdinalIgnoreCase;
}

/// <inheritdoc/>
/// <summary>
/// Constructor.
/// </summary>
/// <param name="stringComparison"></param>
[PublicAPI]
public class MAMEGameIdComparer(StringComparison stringComparison) : IEqualityComparer<MAMEGameId>
{
    private static MAMEGameIdComparer? _default;

    /// <summary>
    /// Default equality comparer that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static MAMEGameIdComparer Default => _default ??= new();

    private readonly StringComparison _stringComparison = stringComparison;

    /// <summary>
    /// Default constructor that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public MAMEGameIdComparer() : this(StringComparison.OrdinalIgnoreCase) { }

    /// <inheritdoc/>
    public bool Equals(MAMEGameId x, MAMEGameId y) => string.Equals(x.Value, y.Value, _stringComparison);

    /// <inheritdoc/>
    public int GetHashCode(MAMEGameId obj) => obj.Value.GetHashCode(_stringComparison);
}
