﻿#region License & Metadata

// The MIT License (MIT)
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// 
// 
// Modified On:  2020/02/24 15:06
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PluginManager.Extensions
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  internal static partial class IEnumerableEx
  {
    #region Methods

    /// <summary>
    ///   Returns all distinct elements of the given source, where "distinctness" is determined
    ///   via a projection and the default equality comparer for the projected type.
    /// </summary>
    /// <remarks>
    ///   This operator uses deferred execution and streams the results, although a set of
    ///   already-seen keys is retained. If a key is seen multiple times, only the first element with
    ///   that key is returned.
    /// </remarks>
    /// <typeparam name="TSource">Type of the source sequence</typeparam>
    /// <typeparam name="TKey">Type of the projected element</typeparam>
    /// <param name="source">Source sequence</param>
    /// <param name="keySelector">Projection for determining "distinctness"</param>
    /// <returns>
    ///   A sequence consisting of distinct elements from the source sequence, comparing them
    ///   by the specified key projection.
    /// </returns>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,
                                                                 Func<TSource, TKey>       keySelector)
    {
      return source.DistinctBy(keySelector, null);
    }

    /// <summary>
    ///   Returns all distinct elements of the given source, where "distinctness" is determined
    ///   via a projection and the specified comparer for the projected type.
    /// </summary>
    /// <remarks>
    ///   This operator uses deferred execution and streams the results, although a set of
    ///   already-seen keys is retained. If a key is seen multiple times, only the first element with
    ///   that key is returned.
    /// </remarks>
    /// <typeparam name="TSource">Type of the source sequence</typeparam>
    /// <typeparam name="TKey">Type of the projected element</typeparam>
    /// <param name="source">Source sequence</param>
    /// <param name="keySelector">Projection for determining "distinctness"</param>
    /// <param name="comparer">
    ///   The equality comparer to use to determine whether or not keys are
    ///   equal. If null, the default equality comparer for <c>TSource</c> is used.
    /// </param>
    /// <returns>
    ///   A sequence consisting of distinct elements from the source sequence, comparing them
    ///   by the specified key projection.
    /// </returns>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,
                                                                 Func<TSource, TKey>       keySelector,
                                                                 IEqualityComparer<TKey>   comparer)
    {
      if (source == null) throw new ArgumentNullException(nameof(source));
      if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

      return _();

      IEnumerable<TSource> _()
      {
        var knownKeys = new HashSet<TKey>(comparer);
        foreach (var element in source)
          if (knownKeys.Add(keySelector(element)))
            yield return element;
      }
    }

    #endregion
  }
}
