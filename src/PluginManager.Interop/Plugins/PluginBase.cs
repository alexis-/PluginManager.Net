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
// Modified On:  2020/02/25 12:41
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading.Tasks;
using PluginManager.Interop.Contracts;
using PluginManager.Interop.Extensions;
using PluginManager.Interop.Sys;

namespace PluginManager.Interop.Plugins
{
  public abstract class PluginBase<TPlugin, IPlugin, ICore> : PerpetualMarshalByRefObject, IPluginBase
    where TPlugin : PluginBase<TPlugin, IPlugin, ICore>, IPlugin
    where IPlugin : class
  {
    #region Properties & Fields - Non-Public

    private ConcurrentDictionary<string, (IpcServerChannel ipcServer, IDisposable disposable)> RegisteredServicesMap { get; } =
      new ConcurrentDictionary<string, (IpcServerChannel, IDisposable)>();
    private ConcurrentDictionary<string, object> ConsumedServiceMap { get; } = new ConcurrentDictionary<string, object>();

    #endregion




    #region Constructors

    protected PluginBase(string channelName)
    {
      ChannelName = channelName;
      RemotingServicesEx.CreateIpcServer<IPlugin, TPlugin>((TPlugin)this, ChannelName);
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
      //RevokeServices();
      //_ipcServer.StopListening(null);

      // TODO: Improve this
      Task.Factory.StartNew(() =>
      {
        Task.Yield();
        Environment.Exit(0);
      });
    }

    #endregion




    #region Properties & Fields - Public

    public ICore                 Service             { get; set; }
    public IPluginManager<ICore> PluginMgr           { get; set; }
    public Guid                  SessionGuid         { get; set; }
    public bool                  IsDevelopmentPlugin { get; set; }

    #endregion




    #region Properties Impl - Public

    /// <inheritdoc />
    public string AssemblyName => GetType().GetAssemblyName();
    /// <inheritdoc />
    public string AssemblyVersion => GetType().GetAssemblyVersion();
    /// <inheritdoc />
    public string ChannelName { get; }

    #endregion




    #region Methods Impl

    /// <inheritdoc />
    public override object InitializeLifetimeService()
    {
      return null;
    }

    public virtual void OnInjected()
    {
      if (SessionGuid == Guid.Empty)
        throw new NullReferenceException($"{nameof(SessionGuid)} is empty");

      if (Service == null)
        throw new NullReferenceException($"{nameof(Service)} is null");

      if (PluginMgr == null)
        throw new NullReferenceException($"{nameof(PluginMgr)} is null");

      PluginInit();
    }

    /// <inheritdoc />
    public virtual void OnServicePublished(string interfaceTypeName)
    {
      // Ignored -- override for desired behavior
    }

    /// <inheritdoc />
    public virtual void OnServiceRevoked(string interfaceTypeName)
    {
      ConsumedServiceMap.TryRemove(interfaceTypeName, out _);
    }

    #endregion




    #region Methods

    public virtual IService GetService<IService>()
      where IService : class
    {
      var svcType = typeof(IService);

      if (svcType.IsInterface == false)
        throw new ArgumentException($"{nameof(IService)} must be an interface");

      var svcTypeName = svcType.FullName;

      if (svcTypeName == null)
        throw new ArgumentException("Invalid type");

      if (ConsumedServiceMap.ContainsKey(svcTypeName))
        return (IService)ConsumedServiceMap[svcTypeName];

      var channelName = PluginMgr.GetService(svcTypeName);

      if (string.IsNullOrWhiteSpace(channelName))
        return null;

      try
      {
        var svc = RemotingServicesEx.ConnectToIpcServer<IService>(channelName);

        ConsumedServiceMap[svcTypeName] = svc;

        return svc;
      }
      catch (RemotingException ex)
      {
        LogError(ex, $"Failed to acquire remoting object for published service {svcTypeName}");
        return null;
      }
    }

    public virtual void PublishService<IService, TService>(TService service, string channelName = null)
      where IService : class
      where TService : PerpetualMarshalByRefObject, IService
    {
      var svcType = typeof(IService);

      if (svcType.IsInterface == false)
        throw new ArgumentException($"{nameof(IService)} must be an interface");

      var svcTypeName = svcType.FullName;

      if (svcTypeName == null)
        throw new ArgumentException("Invalid type");

      if (RegisteredServicesMap.ContainsKey(svcTypeName))
        throw new ArgumentException($"Service {svcTypeName} already registered");

      LogInformation($"Publishing service {svcTypeName}");

      channelName ??= RemotingServicesEx.GenerateIpcServerChannelName();
      var ipcServer = RemotingServicesEx.CreateIpcServer<IService, TService>(service, channelName);

      var unregisterObj = PluginMgr.RegisterService(SessionGuid, svcTypeName, channelName);

      RegisteredServicesMap[svcTypeName] = (ipcServer, unregisterObj);
    }

    public bool RevokeService<IService>()
      where IService : class
    {
      return RevokeService(typeof(IService));
    }

    public bool RevokeService(Type svcType)
    {
      if (svcType.IsInterface == false)
        throw new ArgumentException("Service type must be an interface");

      var svcTypeName = svcType.FullName;

      if (svcTypeName == null)
        throw new ArgumentException("Invalid type");

      if (RegisteredServicesMap.TryRemove(svcTypeName, out var svcData) == false)
        return false;

      LogInformation($"Revoking service {svcTypeName}");

      svcData.disposable.Dispose();
      svcData.ipcServer.StopListening(null);

      return true;
    }

    public void RevokeServices()
    {
      foreach (var svcKeyValue in RegisteredServicesMap)
      {
        var svcType = svcKeyValue.Key;
        var svcData = svcKeyValue.Value;

        LogInformation($"Revoking service {svcType}");

        try
        {
          svcData.ipcServer.StopListening(null);
          svcData.disposable.Dispose();
        }
        catch (Exception ex)
        {
          LogError(ex, $"Exception while stopping published service {svcType}");
        }
      }
    }

    #endregion




    #region Methods Abs

    protected abstract void LogInformation(string message);
    protected abstract void LogError(Exception    ex, string message);

    protected abstract void PluginInit();

    /// <inheritdoc />
    public abstract string Name { get; }

    #endregion
  }
}
