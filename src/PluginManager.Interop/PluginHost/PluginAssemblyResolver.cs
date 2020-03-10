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
// Modified On:  2020/03/05 15:06
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PluginHost;

namespace PluginManager.Interop.PluginHost
{
  /// <summary>
  ///   Implement the <see cref="AppDomain.AssemblyResolve" /> event to load the plugin's and
  ///   its dependencies' assemblies. See <see cref="PluginAssemblyResolver(string, string)" />
  /// </summary>
  [Serializable]
  public class PluginAssemblyResolver
  {
    #region Constructors

    /// <summary>
    ///   Implement the <see cref="AppDomain.AssemblyResolve" /> event to load the plugin's and
    ///   its dependencies' assemblies as provided by
    ///   <paramref name="pluginAndDependenciesAssembliesPath" />
    /// </summary>
    /// <param name="packageRootFolder">
    ///   The root folder underneath which all packages and their
    ///   assemblies are located
    /// </param>
    /// <param name="pluginAndDependenciesAssembliesPath">
    ///   The file path to plugin's and its
    ///   dependencies' assemblies. Paths should be relative to <paramref name="packageRootFolder" />
    /// </param>
    public PluginAssemblyResolver(string packageRootFolder,
                                  string pluginAndDependenciesAssembliesPath)
    {
      AssemblyNamePathMap = new Dictionary<string, string>();
      var pluginAndDependenciesAssembliesPathArr = pluginAndDependenciesAssembliesPath.Split(
        new[] { PluginHostConst.PluginAndDependenciesAssembliesSeparator },
        StringSplitOptions.RemoveEmptyEntries);

      foreach (var assemblyPath in pluginAndDependenciesAssembliesPathArr)
      {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

        if (string.IsNullOrWhiteSpace(assemblyName))
        {
          Console.Error.WriteLine(
            $"PluginAssemblyResolver.Setup: Cannot find assembly file name iin assembly path {assemblyPath}. Skipping, this may affect the plugin's ability to run correctly");
          continue;
        }

        AssemblyNamePathMap[assemblyName] = packageRootFolder + assemblyPath;
      }

      AppDomain.CurrentDomain.AssemblyResolve += ResolvePluginOrDependencyAssembly;
    }

    #endregion




    #region Properties & Fields - Public

    /// <summary>
    /// A map between assembly name and the assembly file path
    /// </summary>
    public Dictionary<string, string> AssemblyNamePathMap { get; }

    #endregion




    #region Methods

    private Assembly ResolvePluginOrDependencyAssembly(object sender, ResolveEventArgs e)
    {
      var assemblyName = e.Name.Split(',').First();

      if (AssemblyNamePathMap.ContainsKey(assemblyName) == false)
        return null;

      var assemblyPath = AssemblyNamePathMap[assemblyName];

      if (File.Exists(assemblyPath) == false)
      {
        Console.Error.WriteLine(
          $"ResolvePluginOrDependencyAssembly: Provided file path for assembly {assemblyName} doesn't exist: {assemblyPath}");
        return null;
      }

      return Assembly.LoadFrom(assemblyPath);
    }

    #endregion
  }
}
