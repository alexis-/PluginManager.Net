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
// Modified On:  2020/02/25 11:32
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Nito.AsyncEx;
using PluginManager.Contracts;
using PluginManager.Interop.Contracts;
using PluginManager.PackageManager.Models;

namespace PluginManager.Models
{
  /// <summary>
  ///   Represents an instance of a plugin. Its process information will be updated if it is
  ///   running.
  /// </summary>
  [Serializable]
  public abstract class PluginInstanceBase<TParent, TMeta, IPlugin>
    : IEquatable<PluginInstanceBase<TParent, TMeta, IPlugin>>, IPluginInstance<TParent, TMeta, IPlugin>
    where IPlugin : IPluginBase
  {
    #region Constructors

    protected PluginInstanceBase(LocalPluginPackage<TMeta> package)
    {
      Package       = package;
      IsDevelopment = Package is LocalDevPluginPackage<TMeta>;
    }

    #endregion




    #region Properties & Fields - Public

    public TMeta Metadata => Package.Metadata;

    #endregion




    #region Properties Impl - Public

    public LocalPluginPackage<TMeta> Package { get; }

    public PluginStatus Status  { get; private set; } = PluginStatus.Stopped;
    public IPlugin      Plugin  { get; private set; }
    public Guid         Guid    { get; private set; }
    public Process      Process { get; set; }

    public AsyncLock             Lock           { get; } = new AsyncLock();
    public AsyncManualResetEvent ConnectedEvent { get; } = new AsyncManualResetEvent(false);

    public ConcurrentDictionary<string, string> InterfaceChannelMap { get; } = new ConcurrentDictionary<string, string>();

    public bool   IsDevelopment { get; }
    public string Denomination  => IsDevelopment ? "development plugin" : "plugin";

    #endregion




    #region Methods Impl

    public override string ToString()
    {
      return $"{Denomination} {Package.Id}";
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj))
        return false;
      if (ReferenceEquals(this, obj))
        return true;
      if (obj.GetType() != GetType())
        return false;

      return Equals((PluginInstanceBase<TParent, TMeta, IPlugin>)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
      return Package != null ? Package.GetHashCode() : 0;
    }

    public bool Equals(PluginInstanceBase<TParent, TMeta, IPlugin> other)
    {
      if (ReferenceEquals(null, other))
        return false;
      if (ReferenceEquals(this, other))
        return true;

      return object.Equals(Package, other.Package);
    }

    public virtual Guid OnStarting()
    {
      Status = PluginStatus.Starting;

      ConnectedEvent.Reset();

      return Guid = Guid.NewGuid();
    }

    public virtual void OnConnected(IPlugin plugin)
    {
      Status = PluginStatus.Connected;
      Plugin = plugin;

      ConnectedEvent.Set();
    }

    public virtual void OnStopping()
    {
      Status = PluginStatus.Stopping;
    }

    public virtual void OnStopped()
    {
      Status  = PluginStatus.Stopped;
      Process = null;
      Plugin  = default;
      Guid    = default;
      
      ConnectedEvent.Set();
    }

    #endregion




    #region Methods

    public static bool operator ==(PluginInstanceBase<TParent, TMeta, IPlugin> left,
                                   PluginInstanceBase<TParent, TMeta, IPlugin> right)
    {
      return Equals(left, right);
    }

    public static bool operator !=(PluginInstanceBase<TParent, TMeta, IPlugin> left,
                                   PluginInstanceBase<TParent, TMeta, IPlugin> right)
    {
      return !Equals(left, right);
    }

    #endregion




    #region Methods Abs

    /// <inheritdoc />
    public abstract bool Equals(TParent other);

    public abstract bool IsEnabled { get; set; }

    #endregion




    #region Events

#pragma warning disable CS0067

    public event PropertyChangedEventHandler PropertyChanged;

#pragma warning restore CS0067

    #endregion
  }
}
