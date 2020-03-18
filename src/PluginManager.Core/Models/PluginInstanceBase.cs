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
// Modified On:  2020/03/10 00:43
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
  /// <inheritdoc cref="IPluginInstance{TParent,TMeta,IPlugin}" />
  [Serializable]
  public abstract class PluginInstanceBase<TParent, TMeta, IPlugin>
    : IEquatable<PluginInstanceBase<TParent, TMeta, IPlugin>>, IPluginInstance<TParent, TMeta, IPlugin>
    where IPlugin : IPluginBase
  {
    #region Constructors

    /// <summary>Initialize this instance</summary>
    /// <param name="package">The local NuGet package corresponding to this Plugin Instance</param>
    protected PluginInstanceBase(LocalPluginPackage<TMeta> package)
    {
      Package       = package;
      IsDevelopment = Package is LocalDevPluginPackage<TMeta>;
    }

    #endregion




    #region Properties & Fields - Public

    /// <summary>The metadata associated with this plugin</summary>
    public TMeta Metadata => Package.Metadata;

    #endregion




    #region Properties Impl - Public

    /// <inheritdoc />
    public LocalPluginPackage<TMeta> Package { get; }

    /// <inheritdoc />
    public PluginStatus Status { get; private set; } = PluginStatus.Stopped;

    /// <inheritdoc />
    public IPlugin Plugin { get; private set; }

    /// <inheritdoc />
    public Guid Guid { get; private set; }

    /// <inheritdoc />
    public Process Process { get; set; }

    /// <inheritdoc />
    public AsyncLock Lock { get; } = new AsyncLock();

    /// <inheritdoc />
    public AsyncManualResetEvent ConnectedEvent { get; } = new AsyncManualResetEvent(false);

    /// <inheritdoc />
    public ConcurrentDictionary<string, string> InterfaceChannelMap { get; } = new ConcurrentDictionary<string, string>();

    /// <inheritdoc />
    public bool IsDevelopment { get; }

    /// <inheritdoc />
    public string Denomination => IsDevelopment ? "development plugin" : "plugin";

    #endregion




    #region Methods Impl

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool Equals(PluginInstanceBase<TParent, TMeta, IPlugin> other)
    {
      if (ReferenceEquals(null, other))
        return false;
      if (ReferenceEquals(this, other))
        return true;

      return Equals(Package, other.Package);
    }

    /// <inheritdoc />
    public virtual Guid OnStarting()
    {
      Guid = Guid.NewGuid();
      Status = PluginStatus.Starting;

      ConnectedEvent.Reset();

      return Guid;
    }

    /// <inheritdoc />
    public virtual void OnConnected(IPlugin plugin)
    {
      Plugin = plugin;
      Status = PluginStatus.Connected;

      ConnectedEvent.Set();
    }

    /// <inheritdoc />
    public virtual void OnStopping()
    {
      Status = PluginStatus.Stopping;
    }

    /// <param name="crashed"></param>
    /// <inheritdoc />
    public virtual void OnStopped(bool crashed)
    {
      Process = null;
      Plugin  = default;
      Guid    = default;
      Status = PluginStatus.Stopped;

      ConnectedEvent.Set();
    }

    #endregion




    #region Methods

    /// <summary>Equality operator</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(PluginInstanceBase<TParent, TMeta, IPlugin> left,
                                   PluginInstanceBase<TParent, TMeta, IPlugin> right)
    {
      return Equals(left, right);
    }

    /// <summary>Inequality operator</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(PluginInstanceBase<TParent, TMeta, IPlugin> left,
                                   PluginInstanceBase<TParent, TMeta, IPlugin> right)
    {
      return !Equals(left, right);
    }

    /// <summary>
    ///   Raises the <see cref="PropertyChanged" /> event for Property
    ///   <paramref name="propName" />
    /// </summary>
    /// <param name="propName"></param>
    protected void OnPropertyChanged(string propName)
    {
      PropertyChangedNotificationInterceptor.Intercept(
        this,
        () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName)),
        propName, null, null);
    }

    /// <summary>
    ///   Called by Fody.PropertyChanged when <see cref="Status" /> changes. Override to
    ///   implement custom behaviour. Calling base method isn't necessary.
    /// </summary>
    protected virtual void OnStatusChanged() { }

    #endregion




    #region Methods Abs

    /// <inheritdoc />
    public abstract bool Equals(TParent other);

    /// <inheritdoc />
    public abstract bool IsEnabled { get; set; }

    #endregion




    #region Events

#pragma warning disable CS0067

    /// <inheritdoc />
    public event PropertyChangedEventHandler PropertyChanged;

#pragma warning restore CS0067

    #endregion
  }
}
