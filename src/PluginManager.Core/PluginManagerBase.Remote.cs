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
// Modified On:  2020/02/24 16:05
// Modified By:  Alexis

#endregion




using System.Runtime.Remoting.Channels.Ipc;
using Anotar.Custom;
using PluginManager.Extensions;
using PluginManager.Interop.Contracts;

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
  {
    #region Properties & Fields - Non-Public

    private string IpcServerChannelName { get; set; }

    private IpcServerChannel IpcServer { get; set; }

    #endregion




    #region Methods

    protected void StartIpcServer()
    {
      LogTo.Debug("Starting Plugin IPC Server");

      // Generate random channel name
      IpcServerChannelName = RemotingServicesEx.GenerateIpcServerChannelName();

      IpcServer =
        RemotingServicesEx.CreateIpcServer<IPluginManager<ICore>, TParent>(
          (TParent)this, IpcServerChannelName);
    }

    protected void StopIpcServer()
    {
      LogTo.Debug("Stopping Plugin IPC Server");

      IpcServer.StopListening(null);
      IpcServer = null;
    }

    #endregion
  }
}
