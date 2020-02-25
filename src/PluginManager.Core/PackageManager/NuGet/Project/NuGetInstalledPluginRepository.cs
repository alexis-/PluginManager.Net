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
// Modified On:  2020/02/24 17:23
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
using PluginManager.Extensions;
using PluginManager.PackageManager.Models;

namespace PluginManager.PackageManager.NuGet.Project
{
  /// <summary>Original from: https://github.com/Wyamio/Wyam/ Copyright (c) 2014 Dave Glick</summary>
  [JsonObject(MemberSerialization.OptIn)]
  internal class NuGetInstalledPluginRepository<TMeta> : IDisposable
  {
    #region Properties & Fields - Non-Public

    protected DirectoryPath _pluginHomeDir;

    protected NuGetPluginInstallSession _currentlyInstallingPlugin = null;

    protected Dictionary<PackageIdentity, LocalPluginPackage<TMeta>>
      _identityPluginMap = new Dictionary<PackageIdentity, LocalPluginPackage<TMeta>>();

    [JsonProperty(PropertyName = "Plugins")]
    private IEnumerable<LocalPluginPackage<TMeta>> ConfigPlugins
    {
      get => _identityPluginMap.Values;
      set => _identityPluginMap = value.ToDictionary(p => p.Identity);
    }

    #endregion




    #region Constructors

    public NuGetInstalledPluginRepository() { }

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
      _identityPluginMap.Values.Concat(_identityPluginMap.Values.SelectMany(p => p.Dependencies)).Distinct();

    public bool IsInstalling => _currentlyInstallingPlugin != null && _currentlyInstallingPlugin.CurrentlyInstalling;

    #endregion




    #region Methods

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

    public LocalPluginPackage<TMeta> FindPluginById(string packageId)
    {
      return _identityPluginMap.FirstOrDefault(kv => kv.Key.Id == packageId).Value;
    }

    public IEnumerable<NuGetPackage> FindPackageById(string packageId)
    {
      return AllPackages.Where(p => p.Id == packageId);
    }

    public List<NuGetPackage> GetPackageAndDependencies(PackageIdentity pi)
    {
      var package = this[pi];

      if (package == null)
        return null;

      var ret = new List<NuGetPackage>()
      {
        package
      };

      ret.AddRange(package.Dependencies);

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

      var project = solution.GetPluginProject((LocalPluginPackage<TMeta>)plugin);

      if (project == null)
        throw new InvalidOperationException($"Could not acquire project for existing plugin {plugin.Id}.");

      return plugin.PackageAndDependenciesExist(project.CreatePackageManager());
    }

    public NuGetPluginInstallSession AddPlugin(PackageIdentity identity,
                                               TMeta           metadata = default)
    {
      if (IsInstalling)
      {
        _currentlyInstallingPlugin.Plugin.AddDependency(identity);

        return null;
      }

      // This is a new top-level installation, so add to the root
      var pkg = new LocalPluginPackage<TMeta>(identity, _pluginHomeDir, metadata);

      _currentlyInstallingPlugin = new NuGetPluginInstallSession(this, pkg);

      return _currentlyInstallingPlugin;
    }

    public bool RemovePlugin(PackageIdentity packageIdentity)
    {
      return _identityPluginMap.Remove(packageIdentity);
    }

    #endregion




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
