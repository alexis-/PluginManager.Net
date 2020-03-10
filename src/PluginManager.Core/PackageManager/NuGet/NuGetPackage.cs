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
// Modified On:  2020/03/09 18:19
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using PluginManager.PackageManager.Models;
using PluginManager.PackageManager.NuGet.Project;

namespace PluginManager.PackageManager.NuGet
{
  /// <summary>Represent the basic information for a NuGet package.</summary>
  [JsonObject(MemberSerialization.OptIn)]
  public class NuGetPackage
    : IEquatable<NuGetPackage>, IComparable<NuGetPackage>, INotifyPropertyChanged
  {
    #region Properties & Fields - Non-Public

    private PackageIdentity _packageIdentity;

    #endregion




    #region Constructors

    /// <summary></summary>
    public NuGetPackage() { }

    /// <summary></summary>
    /// <param name="identity">The package's identity (id and version)</param>
    public NuGetPackage(PackageIdentity identity)
    {
      Id      = identity.Id;
      Version = identity.Version?.ToNormalizedString();
    }

    #endregion




    #region Properties & Fields - Public

    /// <summary>The package id. Setter is needed for Json.net</summary>
    [JsonProperty]
    public string Id { get; private set; }

    /// <summary>
    ///   The package version. Setter is needed for updating and
    ///   <see cref="OnlinePluginPackage{TMeta}" />
    /// </summary>
    [JsonProperty]
    public string Version { get; set; }

    /// <summary>
    ///   Returns a <see cref="PackageIdentity" /> instance built from <see cref="Id" /> and
    ///   <see cref="Version" />
    /// </summary>
    public PackageIdentity Identity => _packageIdentity ??= TryCreatePackageIdentity();

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

    /// <inheritdoc />
    public bool Equals(NuGetPackage other)
    {
      return other != null && Equals(Identity, other.Identity);
    }

    #endregion




    #region Methods

    /// <summary>Called by Fody.PropertyChanged. Always call base if overriden.</summary>
    protected virtual void OnVersionChanged()
    {
      _packageIdentity = null;
    }

    private PackageIdentity TryCreatePackageIdentity()
    {
      NuGetVersion.TryParse(Version, out var nuGetVersion);

      return new PackageIdentity(Id, nuGetVersion);
    }

    /// <summary></summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(NuGetPackage left,
                                   NuGetPackage right)
    {
      return Equals(left, right);
    }

    /// <summary></summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(NuGetPackage left,
                                   NuGetPackage right)
    {
      return !Equals(left, right);
    }

    /// <summary>
    ///   Raises the <see cref="PropertyChanged" /> event for Property
    ///   <paramref name="propName" />
    /// </summary>
    /// <param name="propName"></param>
    protected void OnPropertyChanged(string propName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    /// <summary>
    ///   Some operations (like
    ///   <see
    ///     cref="NuGetPluginProject{TMeta}.UpdatePluginAsync(NuGet.ProjectManagement.INuGetProjectContext, IEnumerable{NuGet.Protocol.Core.Types.SourceRepository}, NuGetVersion, bool, System.Threading.CancellationToken)" />
    ///   ) are run in a different thread which prevents UI updates when
    ///   <see cref="PropertyChanged" /> is raised. This allows the changed event to be propagated
    ///   after the thread has returned to the synchronization context.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal void RaiseVersionChanged()
    {
      OnVersionChanged();
      OnPropertyChanged(nameof(Version));
    }

    #endregion




    #region Events

    /// <inheritdoc />
    public event PropertyChangedEventHandler PropertyChanged;

    #endregion
  }
}
