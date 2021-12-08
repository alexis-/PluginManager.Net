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
// Modified On:  2020/03/09 23:39
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

// ReSharper disable PossibleMultipleEnumeration

namespace PluginManager.PackageManager.Models
{
  /// <summary>
  ///   Represents an online NuGet plugin package. <see cref="OnlinePluginPackage{TMeta}" />
  ///   should only be used from packages that aren't locally installed but are available on a NuGet
  ///   repository.
  /// </summary>
  /// <typeparam name="TMeta"></typeparam>
  public class OnlinePluginPackage<TMeta> : PluginPackage<TMeta>
  {
    #region Constructors

    /// <summary>Create from search results</summary>
    /// <param name="packageId">The package id</param>
    /// <param name="metadata">The associated package metadata</param>
    /// <param name="onlineVersions">
    ///   All available versions for given <paramref name="packageId" />
    /// </param>
    public OnlinePluginPackage(string packageId, TMeta metadata, List<VersionInfo> onlineVersions)
      : base(new PackageIdentity(packageId, null), metadata)
    {
      if (onlineVersions == null)
        throw new ArgumentNullException(nameof(onlineVersions));

      if (onlineVersions.Any() == false)
        throw new ArgumentException($"{nameof(onlineVersions)} cannot be empty");

      SetOnlineVersions(onlineVersions);
    }

    /// <summary>Copy the information from <paramref name="localPackage" /></summary>
    /// <param name="localPackage">The local package to copy from</param>
    public OnlinePluginPackage(LocalPluginPackage<TMeta> localPackage)
      : base(localPackage.Identity, localPackage.Metadata)
    {
      if (localPackage.OnlineVersions == null)
        throw new ArgumentNullException(nameof(localPackage.OnlineVersions));

      if (localPackage.OnlineVersions.Any() == false)
        throw new ArgumentException($"{nameof(localPackage.OnlineVersions)} cannot be empty");

      SetOnlineVersions(localPackage);
    }

    #endregion
  }
}
