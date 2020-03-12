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
// Modified On:  2020/03/04 14:49
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
using NuGet.Versioning;
using PluginManager.Extensions;
using PluginManager.PackageManager.NuGet;
using PluginManager.Sys.Converters;

// ReSharper disable PossibleMultipleEnumeration

namespace PluginManager.PackageManager.Models
{
  /// <summary>
  ///   Represents a locally installed plugin package, as opposed to
  ///   <see cref="OnlinePluginPackage{TMeta}" /> and the plugin's NuGet dependency packages.
  /// </summary>
  /// <typeparam name="TMeta">The metadata to associate with each plugin</typeparam>
  [JsonObject(MemberSerialization.OptIn)]
  public class LocalPluginPackage<TMeta> : PluginPackage<TMeta>, IEquatable<LocalPluginPackage<TMeta>>
  {
    #region Constructors

    /// <inheritdoc />
    public LocalPluginPackage() { }

    /// <summary></summary>
    /// <param name="identity"></param>
    /// <param name="rootHomeDir"></param>
    /// <param name="metadata"></param>
    public LocalPluginPackage(PackageIdentity identity,
                              DirectoryPath   rootHomeDir,
                              TMeta           metadata)
      : base(identity, metadata)
    {
      HomeDir = rootHomeDir.Combine(Id);
    }

    #endregion




    #region Properties & Fields - Public
    
    /// <summary>
    ///   Wrapper around <see cref="Dependencies" /> used solely to store dependencies as a
    ///   list in the .json config. Json.net will first use the getter function of a property to check
    ///   if it already has instantiated a collection; if it finds one, it will add the deserialized
    ///   items to it. <see cref="ObjectCreationHandling.Replace" /> tells Json.net to always
    ///   instantiate a new collection.
    /// </summary>
    [JsonProperty(PropertyName = "Dependencies", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private List<NuGetPackage> ConfigDependencies
    {
      get => Dependencies.Values.ToList();
      set => Dependencies = value.ToDictionary(p => p.Identity);
    }

    /// <summary>All the <see cref="NuGetPackage" /> that this plugin depends on to run its program</summary>
    public Dictionary<PackageIdentity, NuGetPackage> Dependencies { get; private set; } = new Dictionary<PackageIdentity, NuGetPackage>();

    /// <summary>
    ///   All the <see cref="NuGetPackage" /> that this plugin depends on to run its program,
    ///   and the plugin's package itself
    /// </summary>
    public IEnumerable<NuGetPackage> PluginAndDependencies => new List<NuGetPackage> { this }.Concat(Dependencies.Values);

    /// <summary>
    ///   The home folder for this plugin, where it may create and store files needed when
    ///   executing its program. This folder also contains a copy of the content folder bundled in the
    ///   plugin's NuGet package
    /// </summary>
    [JsonProperty]
    [JsonConverter(typeof(DirectoryPathConverter))]
    public virtual DirectoryPath HomeDir { get; private set; }

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

    /// <summary>
    /// Adds a <see cref="NuGetPackage" /> dependency on which this plugin depends
    /// </summary>
    /// <param name="packageIdentity">The dependency package's identity</param>
    /// <returns>The created <see cref="NuGetPackage"/></returns>
    public virtual NuGetPackage AddDependency(PackageIdentity packageIdentity)
    {
      var dependency = new NuGetPackage(packageIdentity);

      Dependencies[packageIdentity] = dependency;

      return dependency;
    }
    
    /// <summary>
    /// Removes the <paramref name="packageIdentity"/> dependency on which this plugin depends
    /// </summary>
    /// <param name="packageIdentity">The dependency package's identity</param>
    /// <returns>The removed <see cref="NuGetPackage"/></returns>
    public virtual NuGetPackage RemoveDependency(PackageIdentity packageIdentity)
    {
      if (Dependencies.TryGetValue(packageIdentity, out var dependency))
      {
        Dependencies.Remove(packageIdentity);

        return dependency;
      }

      return null;
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
      foreach (var package in Dependencies.Values)
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
