using System;
using System.Diagnostics.CodeAnalysis;
using FluentResults;
using GameFinder.Common;
using JetBrains.Annotations;

namespace GameFinder.Wine;

/// <summary>
/// Extension methods.
/// </summary>
[PublicAPI]
public static class Extensions
{
    /// <summary>
    /// Returns <c>true</c> if the result is of type <typeparamref name="TPrefix"/>.
    /// </summary>
    /// <param name="result"></param>
    /// <typeparam name="TPrefix"></typeparam>
    /// <returns></returns>
    public static bool IsPrefix<TPrefix>(this Result<TPrefix> result)
        where TPrefix : AWinePrefix
    {
        return result.IsSuccess;
    }

    /// <summary>
    /// Returns the <typeparamref name="TPrefix"/> part of the result. This can throw if
    /// the result is not of type <typeparamref name="TPrefix"/>. Use <see cref="TryGetPrefix{TPrefix}"/>
    /// instead.
    /// </summary>
    /// <param name="result"></param>
    /// <typeparam name="TPrefix"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is not of type <typeparamref name="TPrefix"/>.
    /// </exception>
    public static TPrefix AsPrefix<TPrefix>(this Result<TPrefix> result)
        where TPrefix : AWinePrefix
    {
        return result.Value;
    }

    /// <summary>
    /// Returns the <typeparamref name="TPrefix"/> part of the result using the try-get
    /// pattern.
    /// </summary>
    /// <param name="result"></param>
    /// <param name="prefix"></param>
    /// <typeparam name="TPrefix"></typeparam>
    /// <returns></returns>
    public static bool TryGetPrefix<TPrefix>(
        this Result<TPrefix> result,
        [MaybeNullWhen(false)] out TPrefix prefix)
        where TPrefix : AWinePrefix
    {
        prefix = null;
        if (!result.IsPrefix()) return false;

        prefix = result.AsPrefix();
        return true;
    }
}
