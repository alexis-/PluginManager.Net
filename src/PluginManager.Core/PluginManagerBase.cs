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
// Modified On:  2020/02/25 00:38
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Anotar.Custom;
using PluginManager.Contracts;
using PluginManager.Extensions;
using PluginManager.Interop.Contracts;
using PluginManager.Interop.Sys;
using PluginManager.Logger;
using PluginManager.Models;
using PluginManager.PackageManager;
using PluginManager.PackageManager.NuGet;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
    : PerpetualMarshalByRefObject, IPluginManager<ICore>, IDisposable
    where TParent : PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>, ICustomPluginManager
    where TPluginInstance : IPluginInstance<TPluginInstance, TMeta, IPlugin>
    where ICustomPluginManager : IPluginManager<ICore>
    where ICore : class
    where IPlugin : IPluginBase
  {
    #region Properties & Fields - Non-Public

    protected readonly ObservableCollection<TPluginInstance>       _allPlugins;
    protected readonly ConcurrentDictionary<string, string>        _interfaceChannelMap;
    protected readonly ConcurrentDictionary<Guid, TPluginInstance> _runningPluginMap;

    #endregion




    #region Constructors

    protected PluginManagerBase(ILogAdapter logger)
    {
      PluginManagerLogger.UserLogger = logger ?? throw new ArgumentNullException(nameof(logger));

      _allPlugins          = new ObservableCollection<TPluginInstance>();
      _interfaceChannelMap = new ConcurrentDictionary<string, string>();
      _runningPluginMap    = new ConcurrentDictionary<Guid, TPluginInstance>();

      AllPlugins = new ReadOnlyObservableCollection<TPluginInstance>(_allPlugins);
      PackageManager = new PluginPackageManager<TMeta>(
        Locations.PluginDir,
        Locations.PluginHomeDir,
        Locations.PluginPackageDir,
        Locations.PluginConfigFile,
        RepoService,
        s => new NuGetSourceRepositoryProvider(s));
    }

    /// <inheritdoc />
    public void Dispose()
    {
      if (IsDisposed)
        throw new InvalidOperationException("Already disposed");

      IsDisposed = true;

      Task.Run(StopPlugins).Wait();

      StopIpcServer();
    }

    #endregion




    #region Properties & Fields - Public

    public ReadOnlyObservableCollection<TPluginInstance> AllPlugins { get; }

    public bool IsDisposed { get; private set; }

    #endregion




    #region Methods

    protected virtual async Task OnStarted()
    {
      LogTo.Debug($"Initializing {GetType().Name}");

      StartIpcServer();
      //StartMonitoringPlugins();

      await RefreshPlugins();
      await StartPlugins();
      
      LogTo.Debug($"Initializing {GetType().Name}... Done");
    }

    protected virtual void OnStopped()
    {
      LogTo.Debug($"Cleaning up {GetType().Name}");

      StopPlugins().Wait();

      LogTo.Debug($"Cleaning up {GetType().Name}... Done");
    }
    
    public async Task StartPlugins()
    {
      try
      {
        LogTo.Information($"Starting all {_allPlugins.Count(p => p.IsEnabled)} enabled plugins out of {_allPlugins.Count}.");

        var plugins = _allPlugins.Where(pi => pi.IsEnabled)
                                 .OrderBy(pi => pi.IsDevelopment)
                                 .DistinctBy(pi => pi.Package.Id);
        var startTasks = plugins.Select(StartPlugin).ToList();

        var startTasksRes = await Task.WhenAll(startTasks);

        var successCount = startTasksRes.Count(success => success);
        var failureCount = startTasksRes.Length - successCount;

        LogTo.Information($"{successCount} started successfully, {failureCount} failed to start.");
      }
      catch (Exception ex)
      {
        LogTo.Warning(ex, "Exception caught while starting plugins.");
        throw;
      }
    }
    
    public async Task StopPlugins()
    {
      try
      {
        LogTo.Information($"Stopping all {_runningPluginMap.Count} running plugins.");

        var stopTasks = _runningPluginMap.Values.Select(StopPlugin);

        await Task.WhenAll(stopTasks);
      }
      catch (Exception ex)
      {
        LogTo.Warning(ex, "Exception caught while stopping plugins.");
        throw;
      }
    }
    
    protected virtual async Task RefreshPlugins()
    {
      try
      {
        LogTo.Information("Refreshing plugins.");

        await StopPlugins();

        _allPlugins.Clear();
        ScanLocalPlugins(true)
          .Select(CreatePluginInstance)
          .Distinct()
          .ForEach(pi => _allPlugins.Add(pi));

        LogTo.Information($"Found {_allPlugins.Count} plugins.");
      }
      catch (Exception ex)
      {
        LogTo.Warning(ex, "Exception caught while refreshing plugins.");
        throw;
      }
    }

    protected virtual void OnPluginStarting(TPluginInstance pluginInstance)
    {
      LogTo.Information($"Starting {pluginInstance.Denomination} {pluginInstance.Package.Id}.");

      _runningPluginMap[pluginInstance.OnStarting()] = pluginInstance;
    }

    protected virtual void OnPluginConnected(TPluginInstance pluginInstance,
                                             IPlugin         plugin)
    {
      LogTo.Information($"Connected {pluginInstance.Denomination} {pluginInstance.Package.Id}.");

      pluginInstance.OnConnected(plugin);
    }

    protected virtual void OnPluginStopping(TPluginInstance pluginInstance)
    {
      LogTo.Information($"Stopping {pluginInstance.Denomination} {pluginInstance.Package.Id}.");

      pluginInstance.OnStopping();
    }

    protected virtual void OnPluginStopped(TPluginInstance pluginInstance)
    {
      if (IsDisposed || pluginInstance.Status == PluginStatus.Stopped)
        return;

      bool crashed = false;
      var exitCode = pluginInstance.Process.ExitCode;

      try
      {
        if (pluginInstance.Process?.HasExited ?? false)
          crashed = pluginInstance.Process.ExitCode != 0;
      }
      catch
      {
        /* ignored */
      }

      LogTo.Information($"{pluginInstance.Denomination.CapitalizeFirst()} {pluginInstance.Package.Id} "
                        + $"has {(crashed ? $"crashed with code {exitCode}" : "stopped")}");

      foreach (var interfaceType in pluginInstance.InterfaceChannelMap.Keys)
        UnregisterChannelType(interfaceType, pluginInstance.Guid, false);

      _runningPluginMap.TryRemove(pluginInstance.Guid, out _);

      pluginInstance.OnStopped();
    }

    #endregion
  }
}
