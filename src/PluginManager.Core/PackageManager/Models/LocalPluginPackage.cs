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
// Modified On:  2020/02/24 17:21
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Linq;
using Anotar.Custom;
using Extensions.System.IO;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using PluginManager.Extensions;
using PluginManager.PackageManager.NuGet;

// ReSharper disable PossibleMultipleEnumeration

namespace PluginManager.PackageManager.Models
{
  [JsonObject(MemberSerialization.OptIn)]
  public class LocalPluginPackage<TMeta> : PluginPackage<TMeta>, IEquatable<LocalPluginPackage<TMeta>>
  {
    #region Constructors

    public LocalPluginPackage() { }

    public LocalPluginPackage(PackageIdentity identity,
                              DirectoryPath   homeDir,
                              TMeta           metadata)
      : base(identity, metadata)
    {
      HomeDir = homeDir.Combine(Id);
    }

    #endregion




    #region Properties & Fields - Public

    [JsonProperty]
    public HashSet<NuGetPackage> Dependencies { get; private set; } = new HashSet<NuGetPackage>();

    public IEnumerable<NuGetPackage> PluginAndDependencies => new List<NuGetPackage> { this }.Concat(Dependencies);

    public virtual DirectoryPath HomeDir { get; }

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

      return Equals((LocalPluginPackage<TMeta>)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
      unchecked
      {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return (base.GetHashCode() * 397) ^ EqualityComparer<TMeta>.Default.GetHashCode(Metadata);
      }
    }

    /// <inheritdoc />
    public bool Equals(LocalPluginPackage<TMeta> other)
    {
      if (ReferenceEquals(null, other))
        return false;
      if (ReferenceEquals(this, other))
        return true;

      return base.Equals(other) && EqualityComparer<TMeta>.Default.Equals(Metadata, other.Metadata);
    }

    #endregion




    #region Methods

    public virtual void SetOnlineVersions(IEnumerable<IPackageSearchMetadata> srl)
    {
      OnlineVersions = srl.Select(sr => sr.Identity.Version);
    }

    public virtual NuGetPackage AddDependency(PackageIdentity packageIdentity)
    {
      var dependency = new NuGetPackage(packageIdentity);

      Dependencies.Add(dependency);

      return dependency;
    }

    public virtual NuGetPackage RemoveDependency(PackageIdentity packageIdentity)
    {
      NuGetPackage dependency = null;

      Dependencies.RemoveWhere(p =>
      {
        if (Equals(p.Identity, packageIdentity))
        {
          dependency = p;
          return true;
        }

        return false;
      });

      return dependency;
    }

    public virtual IEnumerable<FilePath> GetPluginAndDependenciesAssembliesFilePaths(FolderNuGetProject project,
                                                                                     NuGetFramework     targetFramework)
    {
      return PluginAndDependencies.SelectMany(p => p.GetReferencedAssembliesFilePaths(project, targetFramework));
    }

    public virtual IEnumerable<DirectoryPath> GetPluginAndDependenciesContentDirectoryPaths(FolderNuGetProject project,
                                                                                            NuGetFramework     targetFramework)
    {
      return PluginAndDependencies.SelectMany(p => p.GetContentDirectoryPath(project, targetFramework));
    }

    /// <summary>Verifies that the plugin package and all of its dependencies exist locally</summary>
    public virtual bool PackageAndDependenciesExist(NuGetPackageManager packageManager)
    {
      // Check this package
      if (!packageManager.PackageExistsInPackagesFolder(Identity))
      {
        LogTo.Warning(
          $"Cached plugin package {Id} {Version} does not exist in packages folder");

        return false;
      }

      // Check dependencies
      foreach (var package in Dependencies)
        if (!package.PackageExists(packageManager))
        {
          LogTo.Warning(
            $"Cached package dependency {package.Id} {package.Version} "
            + $"of plugin {Id} {Version} does not exist in packages folder");

          return false;
        }

      return true;
    }

    public static bool operator ==(LocalPluginPackage<TMeta> left,
                                   LocalPluginPackage<TMeta> right)
    {
      return Equals(left, right);
    }

    public static bool operator !=(LocalPluginPackage<TMeta> left,
                                   LocalPluginPackage<TMeta> right)
    {
      return !Equals(left, right);
    }

    #endregion
  }
}
