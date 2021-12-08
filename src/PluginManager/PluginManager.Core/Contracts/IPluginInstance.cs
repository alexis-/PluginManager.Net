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
// Modified On:  2020/03/04 14:27
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Nito.AsyncEx;
using PluginManager.Interop.Contracts;
using PluginManager.Models;
using PluginManager.PackageManager.Models;

namespace PluginManager.Contracts
{
  /// <summary>
  ///   Represents an instance of a plugin. Its process information will be updated if it is
  ///   running.
  /// </summary>
  /// <typeparam name="TParent">
  ///   The latest child in the inheritance hierarchy of
  ///   <see cref="IPluginInstance{TParent, TMeta, IPlugin}" />
  /// </typeparam>
  /// <typeparam name="TMeta">The container for the metadata associated with plugin</typeparam>
  /// <typeparam name="IPlugin">The plugin interface</typeparam>
  public interface IPluginInstance<TParent, TMeta, IPlugin> : IEquatable<TParent>, INotifyPropertyChanged
    where IPlugin : IPluginBase
  {
    /// <summary>Information about the installed NuGet package</summary>
    LocalPluginPackage<TMeta> Package { get; }

    /// <summary>Current process status of the plugin (e.g. starting, connected, stopped, etc.)</summary>
    PluginStatus Status { get; }

    /// <summary>An instance of the plugin's remote service. Set when the plugin is running</summary>
    IPlugin Plugin { get; }

    /// <summary>The sessions GUID. Set when the plugin is running</summary>
    Guid Guid { get; }

    /// <summary>The process of the plugin. Set when the plugin is running.</summary>
    Process Process { get; set; }

    /// <summary>
    ///   Synchronizes operations being done on this plugin (e.g. avoids starting the plugin
    ///   several times)
    /// </summary>
    AsyncLock Lock { get; }

    /// <summary>
    ///   This event is triggered when a newly started plugin has connected to the Plugin Manager.
    ///   Used in
    ///   <see
    ///     cref="PluginManagerBase{TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin}.StartPlugin(TPluginInstance)" />
    /// </summary>
    AsyncManualResetEvent ConnectedEvent { get; }

    /// <summary>Services published by the plugin. Maps the interface type to the remote channel name</summary>
    ConcurrentDictionary<string, string> InterfaceChannelMap { get; }

    /// <summary>Whether this plugin is a development plugin</summary>
    bool IsDevelopment { get; }

    /// <summary>e.g. "Plugin", "Development plugin". Used for logging</summary>
    string Denomination { get; }

    /// <summary>Whether this plugin is allowed to be started or not</summary>
    bool IsEnabled { get; }

    /// <summary>
    ///   Called when the plugin is being started, see
    ///   <see
    ///     cref="PluginManagerBase{TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin}.StartPlugin(TPluginInstance)" />
    /// </summary>
    /// <returns>A new session GUID for this plugin's process</returns>
    Guid OnStarting();

    /// <summary>
    ///   Called when the plugin has connected, see
    ///   <see
    ///     cref="PluginManagerBase{TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin}.StartPlugin(TPluginInstance)" />
    /// </summary>
    /// <param name="plugin">The connected plugin's remote service</param>
    void OnConnected(IPlugin plugin);

    /// <summary>
    ///   Called when the plugin is stopping, see
    ///   <see
    ///     cref="PluginManagerBase{TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin}.StartPlugin(TPluginInstance)" />
    /// </summary>
    void OnStopping();

    /// <summary>
    ///   Called when the plugin has stopped, see
    ///   <see
    ///     cref="PluginManagerBase{TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin}.StartPlugin(TPluginInstance)" />
    /// </summary>
    /// <param name="crashed">Whether the plugin stopped with an error Exit Code</param>
    void OnStopped(bool crashed);
  }
}
