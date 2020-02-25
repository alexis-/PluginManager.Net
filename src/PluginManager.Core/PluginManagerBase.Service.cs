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
// Modified On:  2020/02/24 14:50
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Threading.Tasks;
using Anotar.Custom;
using PluginManager.Extensions;
using PluginManager.Models;

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
  {
    #region Methods Impl

    /// <inheritdoc />
    public bool GetAssembliesPathsForPlugin(Guid                    sessionGuid,
                                            out IEnumerable<string> pluginAssemblies,
                                            out IEnumerable<string> dependenciesAssemblies)
    {
      string pluginPackageName = "N/A";
      pluginAssemblies       = null;
      dependenciesAssemblies = null;

      try
      {
        var pluginInstance = _runningPluginMap[sessionGuid];

        if (pluginInstance == null)
        {
          LogTo.Warning($"Plugin {sessionGuid} unexpected for assembly request. Aborting");
          return false;
        }

        pluginPackageName = pluginInstance.Package.Id;

        LogTo.Information($"Fetching assemblies requested by plugin {pluginPackageName}");

        if (pluginInstance.IsDevelopment)
          throw new InvalidOperationException($"Development plugin {pluginPackageName} cannot request assemblies paths");

        var pm = PackageManager;

        lock (pm)
        {
          var pluginPkg = pm.FindInstalledPluginById(pluginPackageName);

          if (pluginPkg == null)
            throw new InvalidOperationException($"Cannot find requested plugin package {pluginPackageName}");

          pm.GetInstalledPluginAssembliesFilePath(
            pluginPkg.Identity,
            out var tmpPluginAssemblies,
            out var tmpDependenciesAssemblies);

          pluginAssemblies       = tmpPluginAssemblies.Select(p => p.FullPath);
          dependenciesAssemblies = tmpDependenciesAssemblies.Select(p => p.FullPath);
        }

        return true;
      }
      catch (Exception ex)
      {
        LogTo.Error(ex, $"An exception occured while returning assemblies path for plugin {pluginPackageName}");

        return false;
      }
    }

    /// <inheritdoc />
    public ICore ConnectPlugin(string channel,
                               Guid   sessionGuid)
    {
      string pluginAssemblyName = "N/A";

      try
      {
        var plugin = RemotingServicesEx.ConnectToIpcServer<IPlugin>(channel);
        pluginAssemblyName = plugin.AssemblyName;

        var pluginInstance = _runningPluginMap.SafeGet(sessionGuid);

        if (pluginInstance == null)
        {
          LogTo.Warning($"Plugin {pluginAssemblyName} unexpected for connection. Aborting");
          return null;
        }

        using (pluginInstance.Lock.Lock())
          OnPluginConnected(pluginInstance, plugin);

        return GetCoreInstance();
      }
      catch (RemotingException ex)
      {
        LogTo.Warning(ex, $"Connection to plugin {pluginAssemblyName} failed.");

        return null;
      }
      catch (Exception ex)
      {
        LogTo.Error(ex, $"An exception occured while connecting plugin {pluginAssemblyName}");

        return null;
      }
    }

    /// <inheritdoc />
    public string GetService(string remoteInterfaceType)
    {
      return _interfaceChannelMap.SafeGet(remoteInterfaceType);
    }

    /// <inheritdoc />
    public IDisposable RegisterService(Guid   sessionGuid,
                                       string remoteServiceType,
                                       string channelName)
    {
      var pluginInst = _runningPluginMap.SafeGet(sessionGuid);

      if (pluginInst == null)
        throw new ArgumentException("Invalid plugin");

      pluginInst.InterfaceChannelMap[remoteServiceType] = channelName;
      _interfaceChannelMap[remoteServiceType]           = channelName;

      Task.Run(() => NotifyServicePublished(remoteServiceType));

      return new PluginChannelDisposer<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>(this, remoteServiceType, sessionGuid);
    }

    #endregion




    #region Methods

    public void UnregisterChannelType(string remoteServiceType,
                                      Guid   sessionGuid,
                                      bool   requireLock)
    {
      IDisposable @lock = null;

      try
      {
        var pluginInst = _runningPluginMap.SafeGet(sessionGuid);

        if (pluginInst == null)
          throw new ArgumentException($"Plugin not found for service {remoteServiceType}");

        if (requireLock)
          @lock = pluginInst.Lock.Lock();

        pluginInst.InterfaceChannelMap.TryRemove(remoteServiceType, out _);
        _interfaceChannelMap.TryRemove(remoteServiceType, out _);

        Task.Run(() => NotifyServiceRevoked(remoteServiceType));
      }
      finally
      {
        @lock?.Dispose();
      }
    }

    private async Task NotifyServicePublished(string remoteServiceType)
    {
      foreach (var pluginInstance in _runningPluginMap.Values)
        using (await pluginInstance.Lock.LockAsync())
        {
          if (pluginInstance.Status != PluginStatus.Connected)
            continue;

          var plugin     = pluginInstance.Plugin;
          var pluginName = pluginInstance.Package.Id;

          try
          {
            plugin.OnServicePublished(remoteServiceType);
          }
          catch (Exception ex)
          {
            LogTo.Error(ex, $"Exception while notifying plugin {pluginName} of published service {remoteServiceType}");
          }
        }
    }

    private async Task NotifyServiceRevoked(string remoteServiceType)
    {
      foreach (var pluginInstance in _runningPluginMap.Values)
        using (await pluginInstance.Lock.LockAsync())
        {
          if (pluginInstance.Status != PluginStatus.Connected)
            continue;

          var plugin     = pluginInstance.Plugin;
          var pluginName = pluginInstance.Package.Id;

          try
          {
            plugin.OnServiceRevoked(remoteServiceType);
          }
          catch (Exception ex)
          {
            LogTo.Error(ex, $"Exception while notifying plugin {pluginName} of revoked service {remoteServiceType}");
          }
        }
    }

    #endregion
  }
}
