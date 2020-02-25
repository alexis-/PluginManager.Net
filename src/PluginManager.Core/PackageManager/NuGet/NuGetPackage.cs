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
// Modified On:  2020/02/24 18:03
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace PluginManager.PackageManager.NuGet
{
  /// <summary>Original from: https://github.com/Wyamio/Wyam/ Copyright (c) 2014 Dave Glick</summary>
  [JsonObject(MemberSerialization.OptIn)]
  public class NuGetPackage
    : IEquatable<NuGetPackage>, IComparable<NuGetPackage>
  {
    #region Properties & Fields - Non-Public

    private PackageIdentity _packageIdentity;

    #endregion




    #region Constructors

    public NuGetPackage() { }

    public NuGetPackage(PackageIdentity identity)
    {
      Id      = identity.Id;
      Version = identity.Version?.ToNormalizedString();
    }

    #endregion




    #region Properties & Fields - Public

    [JsonProperty]
    public string Id { get; set; }

    [JsonProperty]
    public string Version { get; set; }

    public PackageIdentity Identity => _packageIdentity ?? (_packageIdentity = new PackageIdentity(Id, NuGetVersion.Parse(Version)));

    #endregion




    #region Methods Impl

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj))
        return false;
      if (ReferenceEquals(this, obj))
        return true;
      if (obj.GetType() != GetType())
        return false;

      return Equals((NuGetPackage)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
      return Identity != null ? Identity.GetHashCode() : 0;
    }


    /// <inheritdoc />
    public int CompareTo(NuGetPackage other)
    {
      if (ReferenceEquals(this, other))
        return 0;
      if (ReferenceEquals(null, other))
        return 1;

      return Comparer<PackageIdentity>.Default.Compare(Identity, other.Identity);
    }

    public bool Equals(NuGetPackage other)
    {
      return other != null && Equals(Identity, other.Identity);
    }

    #endregion




    #region Methods

    public static bool operator ==(NuGetPackage left,
                                   NuGetPackage right)
    {
      return Equals(left, right);
    }

    public static bool operator !=(NuGetPackage left,
                                   NuGetPackage right)
    {
      return !Equals(left, right);
    }

    #endregion
  }
}
