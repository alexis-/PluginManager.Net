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
// Created On:   2019/03/02 18:29
// Modified On:  2019/03/02 22:06
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PluginHost;
using PluginManager.Interop.Contracts;
using PluginManager.Interop.Extensions;
using PluginManager.Interop.Sys;

namespace PluginManager.Interop.PluginHost
{
  public abstract partial class PluginHostBase<ICore> : PerpetualMarshalByRefObject, IDisposable
  {
    #region Properties & Fields - Non-Public

    private readonly IPluginBase _plugin;

    private bool _hasExited;

    #endregion




    #region Constructors

    protected PluginHostBase(
      string  pluginEntryAssemblyFilePath,
      Guid    sessionGuid,
      string  mgrChannelName,
      Process mgrProcess,
      bool    isDev)
    {
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
    }

    #endregion




    #region Methods

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

    protected virtual void OnPluginMgrStopped()
    {
      Dispose();
      Exit(PluginHostConst.ExitParentExited);
    }

    protected virtual void Exit(int code)
    {
      _hasExited = true;

      Environment.Exit(code);
    }

    #endregion
  }
}
