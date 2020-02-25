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
// Modified On:  2020/02/25 02:22
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
  public interface IPluginInstance<TParent, TMeta, IPlugin> : IEquatable<TParent>, INotifyPropertyChanged
    where IPlugin : IPluginBase
  {
    LocalPluginPackage<TMeta> Package { get; }

    PluginStatus Status  { get; }
    IPlugin      Plugin  { get; }
    Guid         Guid    { get; }
    Process      Process { get; set; }

    AsyncLock             Lock           { get; }
    AsyncManualResetEvent ConnectedEvent { get; }

    ConcurrentDictionary<string, string> InterfaceChannelMap { get; }

    bool   IsDevelopment { get; }
    string Denomination  { get; }
    bool   IsEnabled     { get; }

    Guid OnStarting();
    void OnConnected(IPlugin plugin);
    void OnStopping();
    void OnStopped();
  }
}
