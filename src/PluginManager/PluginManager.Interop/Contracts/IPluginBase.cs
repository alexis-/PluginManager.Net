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
// Modified On:  2020/02/24 14:08
// Modified By:  Alexis

#endregion




using System;
using PluginManager.Interop.Plugins;

namespace PluginManager.Interop.Contracts
{
  /// <summary>
  /// Contract interface that plugins publish as a remote service
  /// </summary>
  public interface IPluginBase : IDisposable
  {
    /// <summary>
    /// The friendly name of the plugin (as opposed to its package name)
    /// </summary>
    string Name            { get; }

    /// <summary>
    /// The plugin's assembly name
    /// </summary>
    string AssemblyName    { get; }

    /// <summary>
    /// The plugin's version (using the assembly's version is recommended)
    /// </summary>
    string AssemblyVersion { get; }

    /// <summary>
    /// The Channel name where the plugin publishes its remote service
    /// </summary>
    string ChannelName     { get; }

    /// <summary>
    /// Called after PluginHost.exe has instantiated an instance of <see cref="IPluginBase"/> and successfully injected its properties (see <see cref="PluginBase{TPlugin,IPlugin,ICore}" />).
    /// </summary>
    void OnInjected();

    /// <summary>
    /// Called when a service is published by another plugin
    /// </summary>
    /// <param name="interfaceTypeName">The name of the type that is published</param>
    void OnServicePublished(string interfaceTypeName);

    /// <summary>
    /// Called when a service is revoked by another plugin
    /// </summary>
    /// <param name="interfaceTypeName">The name of the type that got revoked</param>
    void OnServiceRevoked(string   interfaceTypeName);
  }
}
