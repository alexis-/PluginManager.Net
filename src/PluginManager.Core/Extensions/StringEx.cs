#region License & Metadata

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
// Modified On:  2020/02/24 15:18
// Modified By:  Alexis

#endregion




using System;
using System.ComponentModel;
using System.Linq;

namespace PluginManager.Extensions
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  internal static class StringEx
  {
    #region Methods

    public static string CapitalizeFirst(this string str)
    {
      switch (str)
      {
        case null: throw new ArgumentNullException(nameof(str));
        case "":   throw new ArgumentException($"{nameof(str)} cannot be empty", nameof(str));
        default:   return str.First().ToString().ToUpper() + str.Substring(1);
      }
    }

    public static string After(this string str, string separator)
    {
      var idx = str.IndexOf(separator, StringComparison.Ordinal);

      return idx >= 0
        ? str.Substring(idx + separator.Length)
        : null;
    }

    #endregion
  }
}
