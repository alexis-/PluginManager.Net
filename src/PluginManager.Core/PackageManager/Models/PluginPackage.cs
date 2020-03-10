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




using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using PluginManager.PackageManager.NuGet;
using PropertyChanged;

namespace PluginManager.PackageManager.Models
{
  /// <summary>
  ///   Represents a plugin NuGet package (as opposed to the plugin's NuGet dependency
  ///   packages)
  /// </summary>
  /// <typeparam name="TMeta">The metadata to associate with each plugin</typeparam>
  [JsonObject(MemberSerialization.OptIn)]
  public abstract class PluginPackage<TMeta> : NuGetPackage
  {
    #region Constructors

    /// <inheritdoc />
    protected PluginPackage() { }

    /// <summary></summary>
    /// <param name="identity">The package identity</param>
    /// <param name="metadata">The metadata to associate with this plugin</param>
    protected PluginPackage(PackageIdentity identity, TMeta metadata) : base(identity)
    {
      Metadata = metadata;
    }

    #endregion




    #region Properties & Fields - Public

    /// <summary>The metadata to associate with this plugin</summary>
    [JsonProperty]
    public TMeta Metadata { get; protected set; }

    /// <summary>All the versions of this plugin package available online</summary>
    public IEnumerable<NuGetVersion> OnlineVersions { get; protected set; }

    /// <summary>Download count for the latest version</summary>
    public long LatestVersionDownloadCount { get; protected set; }

    /// <summary>Total download count of all versions accumulated</summary>
    public long TotalDownloadCount { get; protected set; }

    /// <summary>
    ///   Gets the highest version from <see cref="OnlineVersions" /> or
    ///   <see langword="null" />
    /// </summary>
    [DependsOn(nameof(OnlineVersions))]
    public NuGetVersion LatestOnlineVersion => OnlineVersions?.Max();

    /// <summary>
    ///   Checks whether the current version is lower than <see cref="LatestOnlineVersion" />
    /// </summary>
    [DependsOn(nameof(OnlineVersions), nameof(Version))]
    public virtual bool HasPendingUpdates =>
      Identity?.Version != null && LatestOnlineVersion != null && LatestOnlineVersion > Identity?.Version;

    /// <summary>
    ///   Use this property for e.g. selecting which version to install or update to in your
    ///   UI. Unused by the PluginManager.
    /// </summary>
    public NuGetVersion SelectedVersion { get; set; }

    #endregion




    #region Methods Impl

    /// <inheritdoc />
    protected override void OnVersionChanged()
    {
      base.OnVersionChanged();

      OnPropertyChanged(nameof(HasPendingUpdates));
    }

    #endregion




    #region Methods

    /// <summary>
    ///   Set the available online version from the <see cref="IPackageSearchMetadata" />. Used by
    ///   <see
    ///     cref="PluginPackageManager{TMeta}.Search(string, SourceRepository, bool, System.Func{string, TMeta}, System.Func{IPackageSearchMetadata, bool}, System.Threading.CancellationToken)" />
    /// </summary>
    /// <param name="versions">The available versions result</param>
    public virtual void SetOnlineVersions(List<VersionInfo> versions)
    {
      LatestVersionDownloadCount = versions.Aggregate((vi1, vi2) => vi1.Version > vi2.Version ? vi1 : vi2)?.DownloadCount ?? 0;
      TotalDownloadCount         = versions.Sum(vi => vi.DownloadCount ?? 0);
      OnlineVersions             = versions.Select(vi => vi.Version);
      SelectedVersion            = LatestOnlineVersion;
    }
    
    /// <summary>
    ///   Set the available online version from the <see cref="IPackageSearchMetadata" />. Used by
    ///   <see
    ///     cref="PluginPackageManager{TMeta}.Search(string, SourceRepository, bool, System.Func{string, TMeta}, System.Func{IPackageSearchMetadata, bool}, System.Threading.CancellationToken)" />
    /// </summary>
    /// <param name="pluginPackage">The plugin package to copy the online information from</param>
    public virtual void SetOnlineVersions(PluginPackage<TMeta> pluginPackage)
    {
      LatestVersionDownloadCount = pluginPackage.LatestVersionDownloadCount;
      TotalDownloadCount         = pluginPackage.TotalDownloadCount;
      OnlineVersions             = pluginPackage.OnlineVersions;
      SelectedVersion            = LatestOnlineVersion;
    }

    #endregion
  }
}
