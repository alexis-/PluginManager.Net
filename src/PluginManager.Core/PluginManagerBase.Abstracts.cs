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
// Modified On:  2020/03/04 12:03
// Modified By:  Alexis

#endregion




using System.Diagnostics;
using System.Threading;
using PluginManager.Contracts;
using PluginManager.Interop.Contracts;
using PluginManager.Interop.Plugins;
using PluginManager.Logger;
using PluginManager.PackageManager.Models;

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
  {
    #region Methods Abs

    /// <summary>
    ///   A contract interface which returns the paths to various key folders and files used by
    ///   the PluginManager. This should be implemented by project users.
    /// </summary>
    public abstract IPluginLocations Locations { get; }

    /// <summary>
    ///   Mandatory log adapter to forward PluginManager's log output to the application's log
    ///   output
    /// </summary>
    public abstract ILogAdapter LogAdapter { get; }

    /// <summary>
    /// Synchronization context used to synchronize with the UI thread when updating an object from a worker thread
    /// </summary>
    public abstract SynchronizationContext UISynchronizationContext { get; }

    /// <summary>
    ///   Gets the assembly name of the Interop library that implements
    ///   <see cref="IPluginBase" /> or <see cref="PluginBase{TPlugin,IPlugin,ICore}" />) to be loaded
    ///   by PluginHost.exe
    /// </summary>
    /// <param name="pluginInstance">The plugin that will be started</param>
    public abstract string GetPluginHostTypeAssemblyName(TPluginInstance pluginInstance);

    /// <summary>
    ///   Gets the namespace-prepended type name of the class which implements
    ///   <see cref="IPluginBase" /> or <see cref="PluginBase{TPlugin,IPlugin,ICore}" />) to be
    ///   instantiated by PluginHost.exe
    /// </summary>
    /// <param name="pluginInstance">The plugin that will be started</param>
    public abstract string GetPluginHostTypeQualifiedName(TPluginInstance pluginInstance);

    /// <summary>Returns the service that needs to be shared with plugins</summary>
    /// <returns>the service that needs to be shared with plugins</returns>
    public abstract ICore GetCoreInstance();

    /// <summary>
    ///   Instantiates a <typeparamref name="TPluginInstance" /> for <paramref name="package" />
    /// </summary>
    /// <param name="package">The package for which <typeparamref name="TPluginInstance" /> is instantiated</param>
    /// <returns>A <typeparamref name="TPluginInstance" /> instance</returns>
    public abstract TPluginInstance CreatePluginInstance(LocalPluginPackage<TMeta> package);

    /// <summary>
    ///   Instantiates a <typeparamref name="TMeta" /> for package <paramref name="packageName" />
    ///   version <paramref name="fileVersionInfo" />
    /// </summary>
    /// <param name="packageName">The package name for which to create <typeparamref name="TMeta" /></param>
    /// <param name="fileVersionInfo">
    ///   The file version for which to create <typeparamref name="TMeta" />
    /// </param>
    /// <returns>A <typeparamref name="TMeta" /> instance or null</returns>
    public abstract TMeta CreateDevMetadata(string          packageName,
                                            FileVersionInfo fileVersionInfo);

    #endregion
  }
}
