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
// Modified On:  2020/03/09 17:26
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anotar.Custom;
using Extensions.System.IO;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using PluginManager.Extensions;
using PluginManager.PackageManager.Models;

// Original from: https://github.com/Wyamio/Wyam/ Copyright (c) 2014 Dave Glick
namespace PluginManager.PackageManager.NuGet.Project
{
  /// <summary>
  ///   Manages the list of plugins and their dependencies and stores it in .json
  ///   configuration file.
  /// </summary>
  [JsonObject(MemberSerialization.OptIn)]
  public class NuGetInstalledPluginRepository<TMeta> : IDisposable
  {
    #region Properties & Fields - Non-Public

    protected DirectoryPath _pluginHomeDir;

    protected NuGetPluginInstallSession _currentlyInstallingPlugin = null;

    protected Dictionary<PackageIdentity, LocalPluginPackage<TMeta>>
      _identityPluginMap = new Dictionary<PackageIdentity, LocalPluginPackage<TMeta>>();

    /// <summary>
    ///   Wrapper around <see cref="_identityPluginMap" /> used solely to store plugins as a
    ///   list in the .json config. Json.net will first use the getter function of a property to check
    ///   if it already has instantiated a collection; if it finds one, it will add the deserialized
    ///   items to it. <see cref="ObjectCreationHandling.Replace" /> tells Json.net to always
    ///   instantiate a new collection.
    /// </summary>
    [JsonProperty(PropertyName = "Plugins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private List<LocalPluginPackage<TMeta>> ConfigPlugins
    {
      get => _identityPluginMap.Values.ToList();
      set => _identityPluginMap = value.ToDictionary(p => p.Identity);
    }

    /// <summary>
    ///   Packages that have been uninstalled but couldn't be deleted during uninstall. They
    ///   will be deleted at a later time, see <see cref="NuGetDeleteOnRestartManager{TMeta}" />.
    /// </summary>
    [JsonProperty]
    private List<string> PackageDirectoriesMarkedForDeletion { get; set; }

    #endregion




    #region Constructors

    public NuGetInstalledPluginRepository()
    {
      PackageDirectoriesMarkedForDeletion = new List<string>();
    }

    public void Dispose()
    {
      SaveAsync().Wait();
    }

    #endregion




    #region Properties & Fields - Public

    public FilePath FilePath { get; private set; }

    public LocalPluginPackage<TMeta> this[PackageIdentity pi] => _identityPluginMap.SafeGet(pi);

    public IEnumerable<LocalPluginPackage<TMeta>> Plugins => _identityPluginMap.Values;

    public IEnumerable<NuGetPackage> AllPackages =>
      _identityPluginMap.Values.Concat(_identityPluginMap.Values.SelectMany(p => p.Dependencies.Values)).Distinct();

    public bool IsInstalling => _currentlyInstallingPlugin != null && _currentlyInstallingPlugin.CurrentlyInstalling;

    #endregion




    #region Methods

    /// <summary>
    ///   Tries to load the <see cref="NuGetInstalledPluginRepository{TMeta}" /> from an
    ///   existing configuration file. Instantiates a new repository if it fails.
    /// </summary>
    /// <param name="solutionFilePath">The configuration file path</param>
    /// <param name="pluginHomeDir">The root directory underneath which plugins' home are located</param>
    /// <returns></returns>
    public static async Task<NuGetInstalledPluginRepository<TMeta>> LoadAsync(
      FilePath      solutionFilePath,
      DirectoryPath pluginHomeDir)
    {
      NuGetInstalledPluginRepository<TMeta> repo = null;

      if (File.Exists(solutionFilePath.FullPath))
        try
        {
          repo                = await solutionFilePath.DeserializeFromFileAsync<NuGetInstalledPluginRepository<TMeta>>();
          repo.FilePath       = solutionFilePath;
          repo._pluginHomeDir = pluginHomeDir;
        }
        catch (Exception ex)
        {
          LogTo.Error($"Error while reading packages file: {ex.Message}");

          // TODO: Rebuild cache
        }

      return repo ?? new NuGetInstalledPluginRepository<TMeta> { FilePath = solutionFilePath, _pluginHomeDir = pluginHomeDir };
    }

    /// <summary>Saves the repository to file</summary>
    /// <returns></returns>
    public async Task<bool> SaveAsync()
    {
      try
      {
        await this.SerializeToFileAsync(FilePath, Formatting.Indented);
      }
      catch (Exception ex)
      {
        LogTo.Error(ex, "Failed to save Installed Packages Cache");

        return false;
      }

      return true;
    }

    /// <summary>
    ///   Searches for a <see cref="LocalPluginPackage{TMeta}" /> named
    ///   <paramref name="packageId" />. This search doesn't include dependencies.
    /// </summary>
    /// <param name="packageId">The plugin's package id</param>
    /// <returns>
    ///   The <see cref="LocalPluginPackage{TMeta}" /> if it is found or
    ///   <see langword="null" />
    /// </returns>
    public LocalPluginPackage<TMeta> FindPluginById(string packageId)
    {
      return _identityPluginMap.FirstOrDefault(kv => kv.Key.Id == packageId).Value;
    }

    /// <summary>
    ///   Searches for plugins' <see cref="LocalPluginPackage{TMeta}" /> or a dependencies'
    ///   <see cref="NuGetPackage" /> named <paramref name="packageId" />
    /// </summary>
    /// <param name="packageId">The plugin's or dependency's package id</param>
    /// <returns>All <see cref="NuGetPackage" /> matching <paramref name="packageId" /></returns>
    public IEnumerable<NuGetPackage> FindPackageById(string packageId)
    {
      return AllPackages.Where(p => p.Id == packageId);
    }

    public List<NuGetPackage> GetPackageAndDependencies(PackageIdentity pi)
    {
      var package = this[pi];

      if (package == null)
        return null;

      var ret = new List<NuGetPackage>
      {
        package
      };

      ret.AddRange(package.Dependencies.Values);

      return ret;
    }

    public bool ContainsPackage(PackageIdentity packageIdentity)
    {
      return AllPackages.FirstOrDefault(p => Equals(p.Identity, packageIdentity)) != null;
    }

    public bool ContainsPlugin(PackageIdentity pluginIdentity)
    {
      return _identityPluginMap.ContainsKey(pluginIdentity);
    }

    public IEnumerable<LocalPluginPackage<TMeta>> GetPluginsDependingOn(PackageIdentity dependencyPackageIdentity)
    {
      return _identityPluginMap.Values.Where(p => p.Dependencies.ContainsKey(dependencyPackageIdentity));
    }

    /// <summary>
    ///   Verifies that a package has been previously installed as well as currently existing
    ///   locally with all dependencies.
    /// </summary>
    public bool IsPluginInstalled(PackageIdentity            packageIdentity,
                                  NuGetPluginSolution<TMeta> solution)
    {
      var plugin = _identityPluginMap.SafeGet(packageIdentity);

      if (plugin == null)
        return false;

      var project = solution.GetPluginProject(plugin);

      if (project == null)
        throw new InvalidOperationException($"Could not acquire project for existing plugin {plugin.Id}.");

      return plugin.PackageAndDependenciesExist(project.CreatePackageManager());
    }

    /// <summary>Add a new plugin</summary>
    /// <param name="identity"></param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public NuGetPluginInstallSession AddPlugin(PackageIdentity identity,
                                               TMeta           metadata = default)
    {
      if (_identityPluginMap.ContainsKey(identity))
        throw new ArgumentException(
          $"Plugin {identity.Id} {identity.Version.ToNormalizedString()} already exists in Plugin Repository for installing");

      if (IsInstalling)
        throw new InvalidOperationException($"{nameof(AddPlugin)} cannot be called while a plugin is already being installed.");

      // This is a new top-level installation, so add to the root
      var pkg = new LocalPluginPackage<TMeta>(identity, _pluginHomeDir, metadata);

      _currentlyInstallingPlugin = new NuGetPluginInstallSession(this, pkg);

      return _currentlyInstallingPlugin;
    }

    public void UpdatePlugin(string packageId, NuGetVersion newVersion)
    {
      var newPkgIdentity = new PackageIdentity(packageId, newVersion);
      var oldPkgIdentity = _identityPluginMap.Keys.FirstOrDefault(id => id.Id == packageId);

      if (_identityPluginMap.ContainsKey(newPkgIdentity))
        throw new ArgumentException(
          $"Plugin {packageId} {newVersion.ToNormalizedString()} already exists in Plugin Repository for updating");

      if (oldPkgIdentity == null || _identityPluginMap.TryGetValue(oldPkgIdentity, out var pluginPkg) == false)
        throw new ArgumentException($"No such plugin {packageId} in Plugin Repository for updating");

      RemovePlugin(oldPkgIdentity);
      _identityPluginMap[newPkgIdentity] = pluginPkg;
    }

    public bool RemovePlugin(PackageIdentity packageIdentity)
    {
      return _identityPluginMap.Remove(packageIdentity);
    }

    public IReadOnlyList<string> GetPackageDirectoriesMarkedForDeletion()
    {
      return PackageDirectoriesMarkedForDeletion;
    }

    public void AddPackageDirectoryForDeletion(string directory)
    {
      PackageDirectoriesMarkedForDeletion.Add(directory);
    }

    #endregion




    /// <summary>
    ///   Temporary session for installing. The plugin will only be added to the repository if
    ///   the install operation is successful, see <see cref="NuGetPluginInstallSession.Success" />.
    /// </summary>
    public class NuGetPluginInstallSession : IDisposable
    {
      #region Properties & Fields - Non-Public

      private readonly NuGetInstalledPluginRepository<TMeta> _repo;

      #endregion




      #region Constructors

      public NuGetPluginInstallSession(NuGetInstalledPluginRepository<TMeta> repo,
                                       LocalPluginPackage<TMeta>             plugin)
      {
        _repo               = repo;
        Plugin              = plugin;
        CurrentlyInstalling = true;
      }

      public void Dispose()
      {
        CurrentlyInstalling = false;

        if (Success)
          _repo._identityPluginMap.Add(Plugin.Identity, Plugin);
      }

      #endregion




      #region Properties & Fields - Public

      public LocalPluginPackage<TMeta> Plugin { get; }

      public bool CurrentlyInstalling { get; private set; }

      public bool Success { get; set; } = false;

      #endregion
    }
  }
}
