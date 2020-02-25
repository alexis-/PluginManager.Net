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
// Modified On:  2020/02/20 02:53
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extensions.System.IO;
using NuGet.Versioning;
using PluginManager.PackageManager;
using PluginManager.PackageManager.Models;

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
  {
    #region Properties & Fields - Non-Public

    private PluginPackageManager<TMeta> PackageManager { get; }

    #endregion




    #region Methods

    public async Task<bool> InstallPlugin(
      string packageName,
      TMeta pluginMetadata,
      NuGetVersion   version         = null,
      bool           allowPrerelease = false)
    {
      var pm = PackageManager;

      if (pm.FindInstalledPluginById(packageName) != null)
        throw new ArgumentException($"Package {packageName} is already installed");

      var success = await pm.InstallAsync(
        packageName,
        pluginMetadata,
        new VersionRange(version),
        allowPrerelease);

      if (success == false)
        return false;

      var pluginPackage = pm.FindInstalledPluginById(packageName);

      if (pluginPackage == null)
        throw new InvalidOperationException($"Package {packageName} installed successfully but couldn't be found");

      _allPlugins.Add(CreatePluginInstance(pluginPackage));

      return true;
    }

    public async Task UninstallPlugin(TPluginInstance pluginInstance)
    {
      if (pluginInstance.IsDevelopment)
        throw new ArgumentException($"Cannot uninstall a development plugin");

      await StopPlugin(pluginInstance);

      using (await pluginInstance.Lock.LockAsync())
      {
        var pm      = PackageManager;
        var success = await pm.UninstallAsync(pluginInstance.Package);

        if (success)
          _allPlugins.Remove(pluginInstance);
      }
    }

    public async Task<IEnumerable<PluginPackage<TMeta>>> SearchPlugins()
    {
      // TODO: Do.
      return null;
    }

    public IEnumerable<LocalPluginPackage<TMeta>> ScanLocalPlugins(bool includeDev)
    {
      IEnumerable<LocalPluginPackage<TMeta>> plugins = ScanInstalledPlugins();

      if (includeDev == false)
        return plugins;

      var devPlugins = ScanDevelopmentPlugins();

      return devPlugins.Concat(plugins);
    }

    public IEnumerable<LocalPluginPackage<TMeta>> ScanInstalledPlugins()
    {
      var pm = PackageManager;

      lock (pm)
        return pm.GetInstalledPlugins();
    }

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
