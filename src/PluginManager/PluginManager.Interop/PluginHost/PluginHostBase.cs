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
// Created On:   2021/03/24 15:56
// Modified On:  2021/03/25 16:05
// Modified By:  Alexis

#endregion




namespace PluginManager.Interop.PluginHost
{
  using System;
  using System.Diagnostics;
  using System.Threading;
  using System.Threading.Tasks;
  using Contracts;
  using Extensions;
  using global::PluginHost;
  using Sys;

  /// <summary>
  ///   The plugin host is initialized the PluginHost.exe in its own AppDomain. This is
  ///   equivalent to the Startup() function for plugins
  /// </summary>
  public abstract partial class PluginHostBase<ICore> : PerpetualMarshalByRefObject, IDisposable
  {
    #region Properties & Fields - Non-Public

    private readonly IPluginBase _plugin;
    private readonly CancellationTokenSource _cts;
    private bool _hasExited;

    #endregion




    #region Constructors

    /// <summary>Instantiates a new plugin</summary>
    /// <param name="pluginEntryAssemblyFilePath"></param>
    /// <param name="sessionGuid"></param>
    /// <param name="mgrChannelName"></param>
    /// <param name="mgrProcess"></param>
    /// <param name="isDev"></param>
    /// <param name="cts"></param>
    protected PluginHostBase(
      string                  pluginEntryAssemblyFilePath,
      Guid                    sessionGuid,
      string                  mgrChannelName,
      Process                 mgrProcess,
      bool                    isDev,
      CancellationTokenSource cts)
    {
      this._cts = cts;

      // Connect to Plugin Manager
      var pluginMgr = RemotingServicesEx.ConnectToIpcServer<IPluginManager<ICore>>(mgrChannelName);

      if (pluginMgr == null)
      {
        Exit(PluginHostConst.ExitIpcConnectionError);
        return;
      }

      // Setup assembly resolution
      if (isDev)
        AppDomain.CurrentDomain.AssemblyResolve += DevelopmentPluginAssemblyResolver;

      // Load & create plugin
      _plugin = LoadAssembliesAndCreatePluginInstance(pluginEntryAssemblyFilePath);

      if (_plugin == null)
      {
        Exit(PluginHostConst.ExitNoPluginTypeFound);
        return;
      }

      // Connect plugin to Plugin Manager
      var core = pluginMgr.ConnectPlugin(
        _plugin.ChannelName,
        sessionGuid);

      if (core == null)
      {
        Exit(PluginHostConst.ExitCouldNotConnectPlugin);
        return;
      }

      // Inject properties
      InjectPropertyDependencies(_plugin, core, pluginMgr, sessionGuid, isDev);

      _plugin.OnInjected();

      // Start monitoring Plugin Manager process
      if (StartMonitoringPluginMgrProcess(mgrProcess) == false)
        Exit(PluginHostConst.ExitParentExited);
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
      _plugin?.Dispose();
      _cts.Cancel();
    }

    #endregion




    #region Methods

    /// <summary>Creates a new long-running task for <see cref="MonitorPluginMgrProcess(object)" />.</summary>
    /// <param name="pluginMgrProc">The Plugin Manager process to monitor.</param>
    /// <returns>Success</returns>
    protected virtual bool StartMonitoringPluginMgrProcess(Process pluginMgrProc)
    {
      if (pluginMgrProc.HasExited)
        return false;

      Task.Factory.StartNew(MonitorPluginMgrProcess,
                            pluginMgrProc,
                            TaskCreationOptions.LongRunning);

      pluginMgrProc.Exited += (o, ev) => OnPluginMgrStopped();

      return true;
    }

    /// <summary>
    ///   Monitors the Plugin Manager process and shutdowns the loaded Plugin (current process)
    ///   when the PM exits.
    /// </summary>
    /// <param name="param">The Plugin Manager <see cref="Process" />.</param>
    protected virtual void MonitorPluginMgrProcess(object param)
    {
      Process pluginMgrProc = (Process)param;

      try
      {
        while (_hasExited == false && pluginMgrProc.HasExited == false)
        {
          pluginMgrProc.Refresh();

          Thread.Sleep(500);
        }

        if (pluginMgrProc.HasExited)
          OnPluginMgrStopped();
      }
      catch
      {
        OnPluginMgrStopped();
        throw;
      }
    }

    /// <summary>Shuts down the loaded Plugin and exits the current process.</summary>
    protected virtual void OnPluginMgrStopped()
    {
      Dispose();
      Exit(PluginHostConst.ExitParentExited);
    }

    /// <summary>Exit the current process.</summary>
    protected virtual void Exit(int code)
    {
      _hasExited = true;

      Environment.Exit(code);
    }

    #endregion
  }
}
