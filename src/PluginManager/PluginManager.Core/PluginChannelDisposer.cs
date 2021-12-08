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
// Modified On:  2020/02/25 12:26
// Modified By:  Alexis

#endregion




using System;
using PluginManager.Contracts;
using PluginManager.Interop.Contracts;
using PluginManager.Interop.Sys;

namespace PluginManager
{
  internal class PluginChannelDisposer<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
    : PerpetualMarshalByRefObject, IDisposable
    where TParent : PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>, ICustomPluginManager
    where TPluginInstance : IPluginInstance<TPluginInstance, TMeta, IPlugin>
    where ICustomPluginManager : IPluginManager<ICore>
    where ICore : class
    where IPlugin : IPluginBase
  {
    #region Properties & Fields - Non-Public

    private readonly PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin> _pm;

    private readonly string _interfaceType;
    private readonly Guid   _sessionGuid;

    #endregion




    #region Constructors

    /// <inheritdoc />
    public PluginChannelDisposer(PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin> pm,
                                 string                                                                                   interfaceType,
                                 Guid                                                                                     sessionGuid)
    {
      _pm            = pm;
      _interfaceType = interfaceType;
      _sessionGuid   = sessionGuid;
    }

    /// <inheritdoc />
    public void Dispose()
    {
      _pm.UnregisterChannelType(_interfaceType, _sessionGuid, true);
    }

    #endregion
  }
}
