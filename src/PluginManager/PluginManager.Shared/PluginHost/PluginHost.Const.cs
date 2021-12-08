// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginHost.Const.cs" company="">
//   
// </copyright>
// <summary>
//   The plugin host const.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

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
// Created On:   2021/03/24 15:56
// Modified On:  2021/03/25 13:17
// Modified By:  Alexis

#endregion




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
// Created On:   2021/03/24 15:56
// Modified On:  2021/03/25 13:14
// Modified By:  Alexis

#endregion




namespace PluginHost
{
  /// <summary>The plugin host const.</summary>
  public static class PluginHostConst
  {
    #region Constants & Statics

    /// <summary>unknown  error code.</summary>
    public const int ExitUnknownError = -1;

    /// <summary>parameters error code.</summary>
    public const int ExitParameters = 1;

    /// <summary>parent exited error code.</summary>
    public const int ExitParentExited = 2;

    /// <summary>ipc connection error error code.</summary>
    public const int ExitIpcConnectionError = 3;

    /// <summary>could not get assemblies paths error code.</summary>
    public const int ExitCouldNotGetAssembliesPaths = 4;

    /// <summary>no plugin type found error code.</summary>
    public const int ExitNoPluginTypeFound = 5;

    /// <summary>could not connect plugin error code.</summary>
    public const int ExitCouldNotConnectPlugin = 6;

    /// <summary>could not find interop assembly error code.</summary>
    public const int ExitCouldNotFindInteropAssembly = 7;

    /// <summary>could not find plugin assembly error code.</summary>
    public const int ExitCouldNotFindPluginAssembly = 8;

    /// <summary>could not find plugin host assembly error code.</summary>
    public const int ExitCouldNotFindPluginHostAssembly = 9;

    /// <summary>could not find plugin host type error code.</summary>
    public const int ExitCouldNotFindPluginHostType = 10;

    /// <summary>could not create plugin host error code.</summary>
    public const int ExitCouldNotCreatePluginHost = 11;

    /// <summary>The plugin and dependencies assemblies separator.</summary>
    public const string PluginAndDependenciesAssembliesSeparator = ";";

    /// <summary>The plugin manager interop assembly name.</summary>
    public const string PluginManagerInteropAssemblyName = "PluginManager.Interop";

    #endregion
  }
}
