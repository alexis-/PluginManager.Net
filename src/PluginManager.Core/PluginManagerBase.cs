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
// Modified On:  2020/02/27 00:05
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
  /// <summary>
  ///   The main class for dealing with plugins. Inherit this class to implement
  ///   PluginManager.NET in your project.
  /// </summary>
  /// <typeparam name="TParent">
  ///   The latest child in the inheritance hierarchy of
  ///   <see
  ///     cref="PluginManagerBase{TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin}" />
  /// </typeparam>
  /// <typeparam name="TPluginInstance">Represents an instance of a local plugin in-memory</typeparam>
  /// <typeparam name="TMeta">The container for the metadata associated with plugin</typeparam>
  /// <typeparam name="ICustomPluginManager">
  ///   The plugin manager interface to publish as a remote
  ///   service. Use <see cref="IPluginManager{ICore}" /> for default behaviour
  /// </typeparam>
  /// <typeparam name="ICore">The actual service that needs to be published as a remote service</typeparam>
  /// <typeparam name="IPlugin">
  ///   The plugin interface that plugins publish as a remote service. Use
  ///   <see cref="IPluginBase" /> for default behaviour
  /// </typeparam>
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
    : PerpetualMarshalByRefObject, IPluginManager<ICore>, IDisposable
    where TParent : PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>, ICustomPluginManager
    where TPluginInstance : IPluginInstance<TPluginInstance, TMeta, IPlugin>
    where ICustomPluginManager : IPluginManager<ICore>
    where ICore : class
    where IPlugin : IPluginBase
  {
    #region Properties & Fields - Non-Public

    protected ObservableCollection<TPluginInstance>       AllPluginsInternal  { get; }
    protected ConcurrentDictionary<string, string>        InterfaceChannelMap { get; }
    protected ConcurrentDictionary<Guid, TPluginInstance> RunningPluginMap    { get; }

    protected bool IsDisposed { get; private set; }

    #endregion




    #region Constructors

    /// <summary>
    ///   Creates the PluginManagerBase instance. There should only be one instance at all
    ///   times.
    /// </summary>
    protected PluginManagerBase()
    {
      PluginManagerLogger.UserLogger = LogAdapter ?? throw new NullReferenceException(nameof(LogAdapter));

      AllPluginsInternal  = new ObservableCollection<TPluginInstance>();
      InterfaceChannelMap = new ConcurrentDictionary<string, string>();
      RunningPluginMap    = new ConcurrentDictionary<Guid, TPluginInstance>();

      AllPlugins = new ReadOnlyObservableCollection<TPluginInstance>(AllPluginsInternal);
      PackageManager = new PluginPackageManager<TMeta>(
        Locations.PluginDir,
        Locations.PluginHomeDir,
        Locations.PluginPackageDir,
        Locations.PluginConfigFile,
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

    /// <summary>All plugins currently loaded</summary>
    public ReadOnlyObservableCollection<TPluginInstance> AllPlugins { get; }

    #endregion




    #region Methods

    /// <summary>
    ///   Initializes the IPC server, scans for plugins and starts enabled plugins if
    ///   <paramref name="startEnabledPlugins" /> is true.
    /// </summary>
    /// <param name="startEnabledPlugins">Whether to start enabled plugins</param>
    /// <returns></returns>
    /// <exception cref="InvalidProgramException">When already initialized</exception>
    protected virtual async Task Initialize(bool startEnabledPlugins = true)
    {
      LogTo.Debug($"Initializing {GetType().Name}");

      if (IpcServer != null || AllPlugins.Any())
        throw new InvalidProgramException("Initialize called while PluginManagerBase is already initialized");

      StartIpcServer();
      //StartMonitoringPlugins();

      await RefreshPlugins();

      if (startEnabledPlugins)
        await StartPlugins();

      LogTo.Debug($"Initializing {GetType().Name}... Done");
    }

    /// <summary>Stops all running plugins and stops the IPC server.</summary>
    protected virtual void Cleanup()
    {
      LogTo.Debug($"Cleaning up {GetType().Name}");

      StopPlugins().Wait();
      StopIpcServer();

      LogTo.Debug($"Cleaning up {GetType().Name}... Done");
    }

    /// <summary>Starts all enabled plugins.</summary>
    /// <returns>Whether all plugins started successfully</returns>
    public async Task<bool> StartPlugins()
    {
      try
      {
        LogTo.Information($"Starting all {AllPluginsInternal.Count(p => p.IsEnabled)} enabled plugins out of {AllPluginsInternal.Count}.");

        var plugins = AllPluginsInternal.Where(pi => pi.IsEnabled)
                                        .OrderBy(pi => pi.IsDevelopment)
                                        .DistinctBy(pi => pi.Package.Id);
        var startTasks = plugins.Select(StartPlugin).ToList();

        var startTasksRes = await Task.WhenAll(startTasks);

        var successCount = startTasksRes.Count(success => success);
        var failureCount = startTasksRes.Length - successCount;

        LogTo.Information($"{successCount} started successfully, {failureCount} failed to start.");

        return failureCount == 0;
      }
      catch (Exception ex)
      {
        LogTo.Warning(ex, "Exception caught while starting plugins.");
        throw;
      }
    }

    /// <summary>Stops all running plugins</summary>
    /// <returns>Whether all plugins stopped successfully</returns>
    public async Task<bool> StopPlugins()
    {
      try
      {
        LogTo.Information($"Stopping all {RunningPluginMap.Count} running plugins.");

        var stopTasks       = RunningPluginMap.Values.Select(StopPlugin);
        var stopTaskResults = await Task.WhenAll(stopTasks);

        return stopTaskResults.All(s => s);
      }
      catch (Exception ex)
      {
        LogTo.Warning(ex, "Exception caught while stopping plugins.");
        throw;
      }
    }

    /// <summary>Stops all running plugins, clears all loaded plugins, scans and load available plugin</summary>
    /// <returns>The number of plugin loaded</returns>
    protected virtual async Task<int> RefreshPlugins()
    {
      try
      {
        LogTo.Information("Refreshing plugins.");

        await StopPlugins();

        AllPluginsInternal.Clear();
        ScanLocalPlugins(true)
          .Select(CreatePluginInstance)
          .Distinct()
          .ForEach(pi => AllPluginsInternal.Add(pi));

        LogTo.Information($"Found {AllPluginsInternal.Count} plugins.");

        return AllPluginsInternal.Count;
      }
      catch (Exception ex)
      {
        LogTo.Warning(ex, "Exception caught while refreshing plugins.");
        throw;
      }
    }

    /// <summary>
    ///   Called immediately after checking the plugin passes the pre-requisites to be started,
    ///   and before the plugin process has been created. Always call this base method if it is
    ///   inherited.
    /// </summary>
    /// <param name="pluginInstance">The plugin to be started</param>
    protected virtual void OnPluginStarting(TPluginInstance pluginInstance)
    {
      LogTo.Information($"Starting {pluginInstance.Denomination} {pluginInstance.Package.Id}.");

      RunningPluginMap[pluginInstance.OnStarting()] = pluginInstance;
    }

    /// <summary>
    /// Called after the plugin process has been started, and before confirming connection from it.
    /// </summary>
    /// <param name="pluginInstance">The plugin that has been started</param>
    protected virtual void OnPluginStarted(TPluginInstance pluginInstance)
    {
      
    }

    /// <summary>
    ///   Called after the plugin process has been started, and connection has been made with
    ///   this Plugin Manager instance Always call this base method if it is inherited.
    /// </summary>
    /// <param name="pluginInstance">The connected plugin instance</param>
    /// <param name="plugin">The connected plugin service</param>
    protected virtual void OnPluginConnected(TPluginInstance pluginInstance,
                                             IPlugin         plugin)
    {
      LogTo.Information($"Connected {pluginInstance.Denomination} {pluginInstance.Package.Id}.");

      UISynchronizationContext.Send(_ => { pluginInstance.OnConnected(plugin); }, null);
    }

    /// <summary>
    ///   Called immediately after checking the plugin passes the pre-requisites to be stopped,
    ///   and before sending it stop signals. Always call this base method if it is inherited.
    /// </summary>
    /// <param name="pluginInstance">The plugin to be stopped</param>
    protected virtual void OnPluginStopping(TPluginInstance pluginInstance)
    {
      LogTo.Information($"Stopping {pluginInstance.Denomination} {pluginInstance.Package.Id}.");

      pluginInstance.OnStopping();
    }

    /// <summary>
    ///   Called after the plugin has been stopped or killed. Its
    ///   <see cref="IPluginInstance{TParent, TMeta, IPlugin}.Process" /> is still available. Always
    ///   call this base method if it is inherited.
    /// </summary>
    /// <param name="pluginInstance">The plugin that stopped</param>
    protected virtual void OnPluginStopped(TPluginInstance pluginInstance)
    {
      if (IsDisposed || pluginInstance.Status == PluginStatus.Stopped)
        return;

      bool crashed  = false;
      var  exitCode = pluginInstance.Process.ExitCode;

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

      RunningPluginMap.TryRemove(pluginInstance.Guid, out _);

      pluginInstance.OnStopped();
    }

    #endregion
  }
}
