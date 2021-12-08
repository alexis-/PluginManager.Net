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
// Modified On:  2020/02/25 01:02
// Modified By:  Alexis

#endregion




using System;
using CommandLine;

namespace PluginManager.PluginHost
{
  // ReSharper disable once ClassNeverInstantiated.Global
  public class PluginHostParameters
  {
    #region Properties & Fields - Public

    [Option("packageRootFolder")]
    public string PackageRootFolder { get; set; }

    [Option("pluginAndDependenciesAssembliesPath")]
    public string PluginAndDependenciesAssembliesPath { get; set; }

    [Option("pluginHostTypeAssemblyName", Required = true)]
    public string PluginHostTypeAssemblyName { get; set; }

    [Option("pluginHostTypeQualifiedName", Required = true)]
    public string PluginHostTypeQualifiedName { get; set; }

    [Option('n', "packageName", Required = true)]
    public string PackageName { get; set; }

    [Option('h', "home", Required = true)]
    public string HomePath { get; set; }

    [Option('s', "session", Required = true)]
    public string SessionString { get; set; }

    [Option('c', "channel", Required = true)]
    public string ChannelName { get; set; }

    [Option('p', "pid", Required = true)]
    public int ManagerProcessId { get; set; }

    [Option('d', "development")]
    public bool IsDevelopment { get; set; }

    [Option("attachDebugger")]
    public bool AttachDebugger { get; set; }

    public Guid SessionGuid => Guid.Parse(SessionString);

    #endregion
  }
}
