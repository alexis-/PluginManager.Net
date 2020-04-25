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
// Modified On:  2020/02/29 14:49
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensions.System.IO;
using Nito.AsyncEx;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using PluginManager.Contracts;
using PluginManager.PackageManager;
using PluginManager.PackageManager.Models;
// ReSharper disable InconsistentNaming

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
  {
    #region Properties & Fields - Non-Public

    /// <summary>
    /// The package manager instance
    /// </summary>
    protected PluginPackageManager<TMeta> PackageManager { get; private set; }

    /// <summary>
    /// Synchronizes calls to the Package Manager (e.g. to avoid installing two plugins at the same time)
    /// </summary>
    protected AsyncReaderWriterLock PMLock { get; } = new AsyncReaderWriterLock();

    #endregion




    #region Methods

    /// <summary>
    ///   Installs the latest online version of package <paramref name="onlinePackage" />.
    ///   Package's version and metadata will be derived from
    ///   <paramref name="onlinePackage.LatestOnlineVersion" /> and
    ///   <paramref name="onlinePackage.Metadata" />. Package's data, and metadata will be saved to
    ///   the package json configuration file.
    /// </summary>
    /// <param name="onlinePackage">The online package to install</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="onlinePackage" /> is already installed</exception>
    /// <exception cref="InvalidOperationException">
    ///   NuGet returned a successful result but the plugin
    ///   cannot be found
    /// </exception>
    public async Task<bool> InstallPluginAsync(
      OnlinePluginPackage<TMeta> onlinePackage,
      CancellationToken          cancellationToken = default)
    {
      using (await PMLock.WriterLockAsync())
      {
        var success = await PackageManager.InstallAsync(
          onlinePackage,
          null,
          cancellationToken);

        return success && PostInstallPlugin(onlinePackage.Id);
      }
    }

    /// <summary>
    ///   Installs package <paramref name="onlinePackage" /> version
    ///   <paramref name="onlineVersion" />. Package data, and
    ///   <paramref name="onlinePackage.Metadata" /> will be saved to the package json configuration
    ///   file.
    /// </summary>
    /// <param name="onlinePackage">The online package to install</param>
    /// <param name="onlineVersion">The version of the package to install</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="onlinePackage" /> is already installed</exception>
    /// <exception cref="InvalidOperationException">
    ///   NuGet returned a successful result but the plugin
    ///   cannot be found
    /// </exception>
    public async Task<bool> InstallPluginAsync(
      OnlinePluginPackage<TMeta> onlinePackage,
      NuGetVersion               onlineVersion     = null,
      CancellationToken          cancellationToken = default)
    {
      using (await PMLock.WriterLockAsync())
      {
        var success = await PackageManager.InstallAsync(
          onlinePackage,
          onlineVersion,
          null,
          cancellationToken);

        return success && PostInstallPlugin(onlinePackage.Id);
      }
    }

    /// <summary>
    ///   Install plugin <paramref name="packageName" />. Latest available version will be
    ///   installed if <paramref name="version" /> is <see langword="null" />.
    /// </summary>
    /// <param name="packageName">The package name of the plugin to be installed</param>
    /// <param name="pluginMetadata">Optional metadata to associate with the plugin</param>
    /// <param name="version">Optional specific version to install</param>
    /// <param name="allowPrerelease">Whether to include versions marked as pre-release</param>
    /// <returns>Success status of installation</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"><paramref name="packageName" /> contains only white spaces</exception>
    /// <exception cref="ArgumentException">
    ///   A package named <paramref name="packageName" /> is already
    ///   installed
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   NuGet returned a successful result but the plugin
    ///   cannot be found
    /// </exception>
    public async Task<bool> InstallPluginAsync(
      string       packageName,
      TMeta        pluginMetadata  = default,
      NuGetVersion version         = null,
      bool         allowPrerelease = false)
    {
      using (await PMLock.WriterLockAsync())
      {
        var success = await PackageManager.InstallAsync(
          packageName,
          pluginMetadata,
          new VersionRange(version, true, version, true),
          allowPrerelease);

        return success && PostInstallPlugin(packageName);
      }
    }

    /// <summary>
    ///   Verifies that <paramref name="packageName" /> can be found in the local packages, and
    ///   adds it to the list of loaded plugins. Called by InstallPluginAsync() after a
    ///   successful Install result from the <see cref="PackageManager" />.
    ///   (!!) A lock on PMLock should have been acquired before calling this method.
    /// </summary>
    /// <param name="packageName">The installed package name</param>
    /// <returns>Whether the package has been successfully installed</returns>
    private bool PostInstallPlugin(string packageName)
    {
      var pluginPackage = PackageManager.FindInstalledPluginById(packageName);

      if (pluginPackage == null)
        throw new InvalidOperationException($"Package {packageName} installed successfully but couldn't be found");

      AllPluginsInternal.Add(CreatePluginInstance(pluginPackage));

      return true;
    }

    /// <summary>
    ///   Attempts to update plugin <paramref name="pluginPackage" />. If no
    ///   <paramref name="version" /> is specified, the latest available version will be installed.
    /// </summary>
    /// <param name="pluginPackage">The plugin package to update</param>
    /// <param name="version">Optional version to install</param>
    /// <param name="allowPrereleaseVersions">Whether to include pre-release versions</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="pluginPackage" /> cannot be a development plugin</exception>
    /// <exception cref="ArgumentException">Plugin <paramref name="pluginPackage" /> is not installed</exception>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<bool> UpdatePluginAsync(
      LocalPluginPackage<TMeta> pluginPackage,
      NuGetVersion              version                 = null,
      bool                      allowPrereleaseVersions = false,
      CancellationToken         cancellationToken       = default)
    {
      var pluginInstance = AllPlugins.FirstOrDefault(pi => pi.Package == pluginPackage);

      if (await StopPlugin(pluginInstance) == false)
      {
        LogAdapter.Warning($"Failed to stop plugin {pluginPackage.Id}, aborting update.");
        return false;
      }

      using (await PMLock.WriterLockAsync())
      {
        var success = await PackageManager.UpdateAsync(
          pluginPackage,
          version,
          allowPrereleaseVersions,
          cancellationToken);

        UISynchronizationContext.Send(pluginPackage.RaiseVersionChanged);

        return success;
      }
    }

    /// <summary>
    ///   Attempts to update plugin <paramref name="packageId" />. If no
    ///   <paramref name="version" /> is specified, the latest available version will be installed.
    /// </summary>
    /// <param name="packageId">The plugin package id to update</param>
    /// <param name="version">Optional version to install</param>
    /// <param name="allowPrereleaseVersions">Whether to include pre-release versions</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="packageId" /> is empty</exception>
    /// <exception cref="ArgumentException">Plugin <paramref name="packageId" /> is not installed</exception>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<bool> UpdatePluginAsync(
      string            packageId,
      NuGetVersion      version                 = null,
      bool              allowPrereleaseVersions = false,
      CancellationToken cancellationToken       = default)
    {
      var pluginInstance = AllPlugins.FirstOrDefault(pi => pi.Package.Id == packageId);

      if (await StopPlugin(pluginInstance) == false)
      {
        LogAdapter.Warning($"Failed to stop plugin {packageId}, aborting update.");
        return false;
      }

      using (await PMLock.WriterLockAsync())
      {
        var success = await PackageManager.UpdateAsync(
          packageId,
          version,
          allowPrereleaseVersions,
          cancellationToken);
        
        UISynchronizationContext.Send(() => PackageManager.FindInstalledPluginById(packageId)?.RaiseVersionChanged());

        return success;
      }
    }

    /// <summary>Uninstall <paramref name="pluginPackage" />. The plugin will be stopped if it is running.</summary>
    /// <param name="pluginPackage">The plugin to be uninstalled</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">
    ///   When <paramref name="pluginPackage" /> is a Development
    ///   plugin
    /// </exception>
    public Task<bool> UninstallPluginAsync(LocalPluginPackage<TMeta> pluginPackage)
    {
      if (pluginPackage == null)
        throw new ArgumentNullException(nameof(pluginPackage));

      if (pluginPackage is LocalDevPluginPackage<TMeta>)
        throw new ArgumentException($"Cannot uninstall a development plugin");

      var pluginInstance = AllPlugins.FirstOrDefault(pi => pi.Package == pluginPackage);

      if (pluginInstance == null)
        throw new InvalidOperationException($"Cannot find a matching PluginInstance for LocalPluginPackage {pluginPackage.Id}");

      return UninstallPluginAsync(pluginInstance);
    }

    /// <summary>Uninstall <paramref name="pluginInstance" />. The plugin will be stopped if it is running.</summary>
    /// <param name="pluginInstance">The plugin to be uninstalled</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">
    ///   When <paramref name="pluginInstance" /> is a Development
    ///   plugin
    /// </exception>
    public async Task<bool> UninstallPluginAsync(TPluginInstance pluginInstance)
    {
      if (pluginInstance == null)
        throw new ArgumentNullException(nameof(pluginInstance));

      if (pluginInstance.IsDevelopment)
        throw new ArgumentException($"Cannot uninstall a development plugin");

      if (await StopPlugin(pluginInstance) == false)
      {
        LogAdapter.Warning($"Failed to stop plugin {pluginInstance.Package.Id}, aborting uninstallation.");
        return false;
      }

      bool success;

      using (await pluginInstance.Lock.LockAsync())
      using (await PMLock.WriterLockAsync())
      {
        success = await PackageManager.UninstallAsync(pluginInstance.Package);

        if (success)
          AllPluginsInternal.Remove(pluginInstance);
      }

      return success;
    }

    /// <summary>
    ///   Search available NuGet repositories for all packages matching
    ///   <paramref name="searchTerm" /> and <paramref name="enablePrerelease" />. Only NuGet packages
    ///   that are also available on the <paramref name="repoSvc" /> will be included.
    /// </summary>
    /// <param name="searchTerm">Part or totality of the package name to look for</param>
    /// <param name="enablePrerelease">Whether to include packages that are marked as pre-release</param>
    /// <param name="repoSvc">Your plugin repository service</param>
    /// <param name="cancellationToken"></param>
    /// <returns>All available packages or <see langword="null" /></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<IEnumerable<PluginPackage<TMeta>>> SearchPluginsAsync(
      string                          searchTerm,
      bool                            enablePrerelease,
      IPluginRepositoryService<TMeta> repoSvc,
      CancellationToken               cancellationToken = default)
    {
      if (repoSvc == null)
        throw new ArgumentNullException(nameof(repoSvc));

      using (await PMLock.ReaderLockAsync())
        return await repoSvc.SearchPlugins(searchTerm, enablePrerelease, PackageManager, cancellationToken);
    }

    /// <summary>
    ///   Search available NuGet repositories for all packages matching
    ///   <paramref name="searchTerm" />, <paramref name="enablePrerelease" />, and
    ///   <paramref name="filterSearchResultFunc" /> (if provided).
    /// </summary>
    /// <param name="searchTerm">Part or totality of the package name to look for</param>
    /// <param name="enablePrerelease">Whether to include packages that are marked as pre-release</param>
    /// <param name="getMetaFromPackageNameFunc">
    ///   Associates an optional <typeparamref name="TMeta" /> with each
    ///   package
    /// </param>
    /// <param name="filterSearchResultFunc">Filters through the search result</param>
    /// <param name="cancellationToken"></param>
    /// <returns>All available packages or <see langword="null" /></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<IEnumerable<PluginPackage<TMeta>>> SearchPluginsAsync(
      string                             searchTerm,
      bool                               enablePrerelease,
      Func<string, TMeta>                getMetaFromPackageNameFunc = null,
      Func<IPackageSearchMetadata, bool> filterSearchResultFunc     = null,
      CancellationToken                  cancellationToken          = default)
    {
      using (await PMLock.ReaderLockAsync())
        return await PackageManager.Search(searchTerm,
                                           enablePrerelease,
                                           getMetaFromPackageNameFunc,
                                           filterSearchResultFunc,
                                           cancellationToken);
    }

    /// <summary>Scan local repositories for installed plugins.</summary>
    /// <param name="includeDev">Whether to include development plugin in the scan</param>
    /// <returns>Local plugins (NuGet plugins, and optionally development plugins)</returns>
    public IEnumerable<LocalPluginPackage<TMeta>> ScanLocalPlugins(bool includeDev)
    {
      IEnumerable<LocalPluginPackage<TMeta>> plugins = ScanInstalledPlugins();

      if (includeDev == false)
        return plugins;

      var devPlugins = ScanDevelopmentPlugins();

      return devPlugins.Concat(plugins);
    }

    /// <summary>
    ///   Scan for installed plugins only (those installed with NuGet). That doesn't include
    ///   development plugins.
    /// </summary>
    /// <returns>Installed plugins</returns>
    public IEnumerable<LocalPluginPackage<TMeta>> ScanInstalledPlugins()
    {
      var pm = PackageManager;

      using (PMLock.ReaderLock())
        return pm.GetInstalledPlugins();
    }

    /// <summary>
    ///   Scan for development plugins only. That doesn't include plugins installed through
    ///   NuGet.
    /// </summary>
    /// <returns>installed NuGet plugins</returns>
    public List<LocalPluginPackage<TMeta>> ScanDevelopmentPlugins()
    {
      var devPlugins = new List<LocalPluginPackage<TMeta>>();
      var devDir     = Locations.PluginDevelopmentDir;

      if (devDir.Exists() == false)
      {
        devDir.Create();
        return devPlugins;
      }

      foreach (DirectoryPath devPluginDir in Directory.EnumerateDirectories(devDir.FullPath))
      {
        var pluginPkg = LocalDevPluginPackage<TMeta>.Create(
          devPluginDir.Segments.Last(),
          Locations.PluginDevelopmentDir,
          CreateDevMetadata);

        if (pluginPkg == null)
          continue;

        devPlugins.Add(pluginPkg);
      }

      return devPlugins;
    }

    #endregion
  }
}
