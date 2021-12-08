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
// Modified On:  2020/02/27 00:28
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
    public ICore ConnectPlugin(string channel,
                               Guid   sessionGuid)
    {
      if (channel == null)
        throw new ArgumentNullException(nameof(channel));

      if (sessionGuid == null)
        throw new ArgumentNullException(nameof(sessionGuid));

      string pluginAssemblyName = "N/A";

      try
      {
        var plugin = RemotingServicesEx.ConnectToIpcServer<IPlugin>(channel);
        pluginAssemblyName = plugin.AssemblyName;

        var pluginInstance = RunningPluginMap.SafeGet(sessionGuid);

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
      if (remoteInterfaceType == null)
        throw new ArgumentNullException(nameof(remoteInterfaceType));

      return InterfaceChannelMap.SafeGet(remoteInterfaceType);
    }

    /// <inheritdoc />
    public IDisposable RegisterService(Guid   sessionGuid,
                                       string remoteServiceType,
                                       string channelName)
    {
      if (sessionGuid == null)
        throw new ArgumentNullException(nameof(sessionGuid));

      if (remoteServiceType == null)
        throw new ArgumentNullException(nameof(remoteServiceType));

      if (channelName == null)
        throw new ArgumentNullException(nameof(channelName));

      var pluginInst = RunningPluginMap.SafeGet(sessionGuid);

      if (pluginInst == null)
        throw new ArgumentException($"No plugin matching session guid {sessionGuid} could be found");

      pluginInst.InterfaceChannelMap[remoteServiceType] = channelName;
      InterfaceChannelMap[remoteServiceType]            = channelName;

      Task.Run(() => NotifyServicePublished(remoteServiceType));

      return new PluginChannelDisposer<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>(
        this, remoteServiceType, sessionGuid);
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
        var pluginInst = RunningPluginMap.SafeGet(sessionGuid);

        if (pluginInst == null)
          throw new ArgumentException($"Plugin not found for service {remoteServiceType}");

        if (requireLock)
          @lock = pluginInst.Lock.Lock();

        pluginInst.InterfaceChannelMap.TryRemove(remoteServiceType, out _);
        InterfaceChannelMap.TryRemove(remoteServiceType, out _);

        Task.Run(() => NotifyServiceRevoked(remoteServiceType));
      }
      finally
      {
        @lock?.Dispose();
      }
    }

    private async Task NotifyServicePublished(string remoteServiceType)
    {
      foreach (var pluginInstance in RunningPluginMap.Values)
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
      foreach (var pluginInstance in RunningPluginMap.Values)
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
