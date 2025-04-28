using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace GameCollector.EmuHandlers.Dolphin;

/// <summary>
/// Represents an id for ROMs for Dolphin Emulator.
/// </summary>
[ValueObject<string>]
public readonly partial struct DolphinGameId : IAugmentWith<DefaultEqualityComparerAugment>
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
public class DolphinGameIdComparer(StringComparison stringComparison) : IEqualityComparer<DolphinGameId>
{
    private static DolphinGameIdComparer? _default;

    /// <summary>
    /// Default equality comparer that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public static DolphinGameIdComparer Default => _default ??= new();

    private readonly StringComparison _stringComparison = stringComparison;

    /// <summary>
    /// Default constructor that uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public DolphinGameIdComparer() : this(StringComparison.OrdinalIgnoreCase) { }

    /// <inheritdoc/>
    public bool Equals(DolphinGameId x, DolphinGameId y) => string.Equals(x.Value, y.Value, _stringComparison);

    /// <inheritdoc/>
    public int GetHashCode(DolphinGameId obj) => obj.Value.GetHashCode(_stringComparison);
}
