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
// Modified On:  2021/03/25 10:24
// Modified By:  Lila

#endregion




namespace PluginHost.Plugin
{
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Threading;
  using Exceptions;
  using PluginManager.PluginHost;

  public static class PluginLoader
  {
    /// <summary>
    ///   Instantiate the PluginHost in a new AppDomain. The newly created AppDomain doesn't
    ///   load the PluginHost.exe assembly nor any other assembly than those on which the plugin
    ///   depends.
    /// </summary>
    /// <param name="args">The PluginHost start parameters</param>
    /// <param name="cts">The cancellation token to release the wait handle on the main thread</param>
    public static IDisposable Create(
      PluginHostParameters args,
      CancellationTokenSource cts)
    {
      Process pluginMgrProcess;

      if (args.AttachDebugger || File.Exists(Path.Combine(args.HomePath, "debugger")))
        Debugger.Launch();

      try
      {
        pluginMgrProcess = Process.GetProcessById(args.ManagerProcessId);
      }
      catch (Exception)
      {
        throw new PluginHostException(PluginHostConst.ExitParentExited);
      }

      return Create(
        args.PackageRootFolder,
        args.PluginAndDependenciesAssembliesPath,
        args.PluginHostTypeAssemblyName,
        args.PluginHostTypeQualifiedName,
        args.PackageName,
        args.HomePath,
        args.SessionGuid,
        args.ChannelName,
        pluginMgrProcess,
        args.IsDevelopment,
        cts);
    }



    #region Methods

    /// <summary>
    ///   Instantiate the PluginHost in a new AppDomain. The newly created AppDomain doesn't
    ///   load the PluginHost.exe assembly nor any other assembly than those on which the plugin
    ///   depends.
    /// </summary>
    /// <param name="packageRootFolder">
    ///   The root folder underneath which all packages and their
    ///   assemblies are located
    /// </param>
    /// <param name="pluginAndDependenciesAssembliesPath">
    ///   The file path to plugin's and its
    ///   dependencies' assemblies. Paths should be relative to <paramref name="packageRootFolder" />
    /// </param>
    /// <param name="pluginHostTypeAssemblyName">
    ///   The name of assembly in which the PluginHost type's
    ///   assembly is defined
    /// </param>
    /// <param name="pluginHostTypeQualifiedName">The namespace-qualified name for the PluginHost type</param>
    /// <param name="pluginPackageName">The plugin's package name</param>
    /// <param name="pluginHomeDir">The home directory of the plugin</param>
    /// <param name="sessionGuid">The session guid used to authenticate with the Plugin Manager</param>
    /// <param name="mgrChannelName">The Plugin Manager's remote service channel name</param>
    /// <param name="mgrProcess">The Plugin Manager's process</param>
    /// <param name="isDev">Whether the plugin is a development plugin</param>
    /// <param name="cts">The cancellation token to release the wait handle on the main thread</param>
    /// <returns>The instantiated plugin cast to <see cref="IDisposable" /></returns>
    public static IDisposable Create(string                  packageRootFolder,
                                     string                  pluginAndDependenciesAssembliesPath,
                                     string                  pluginHostTypeAssemblyName,
                                     string                  pluginHostTypeQualifiedName,
                                     string                  pluginPackageName,
                                     string                  pluginHomeDir,
                                     Guid                    sessionGuid,
                                     string                  mgrChannelName,
                                     Process                 mgrProcess,
                                     bool                    isDev,
                                     CancellationTokenSource cts)
    {
      if (isDev == false)
        CreateAssemblyResolver(
          packageRootFolder,
          pluginAndDependenciesAssembliesPath);

      string pluginEntryAssemblyFilePath =
        FindAssemblyFilePath(isDev,
                             pluginHomeDir,
                             pluginPackageName,
                             packageRootFolder,
                             pluginAndDependenciesAssembliesPath);

      if (string.IsNullOrWhiteSpace(pluginEntryAssemblyFilePath))
      {
        Console.Error.WriteLine(
          $"Unable to find {pluginPackageName} assembly file path "
          + $"in assembly list {pluginAndDependenciesAssembliesPath}");

        throw new PluginHostException(PluginHostConst.ExitCouldNotFindPluginAssembly);
      }

      Type pluginHostType;
      
      try
      {
        pluginHostType = LoadPluginHostType(pluginHostTypeAssemblyName, pluginHostTypeQualifiedName);

        if (pluginHostType == null)
        {
          Console.Error.WriteLine(
            $"Unable to find type {pluginHostTypeQualifiedName} in assembly {pluginHostTypeAssemblyName}.");

          throw new PluginHostException(PluginHostConst.ExitCouldNotFindPluginHostType);
        }
      }
      catch (FileNotFoundException ex)
      {
        Console.Error.WriteLine(
          $"Could not find {pluginHostTypeAssemblyName} assembly file path:\r\n{ex}.");

        throw new PluginHostException(PluginHostConst.ExitCouldNotFindPluginHostAssembly);
      }
      catch (FileLoadException ex)
      {
        Console.Error.WriteLine(
          $"Could not load {pluginHostTypeAssemblyName} assembly file path:\r\n{ex}");

        throw new PluginHostException(PluginHostConst.ExitCouldNotFindPluginHostAssembly);
      }

      try
      {
        return (IDisposable)Activator.CreateInstance(
          pluginHostType,
          BindingFlags.Public | BindingFlags.Instance,
          null,
          new object[]
          {
          pluginEntryAssemblyFilePath,
          sessionGuid,
          mgrChannelName,
          mgrProcess,
          isDev,
          cts
          },
          null,
          null
        );
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine(
          $"Could not create Plugin Host type '{pluginHostTypeQualifiedName}':\r\n{ex}");

        throw new PluginHostException(PluginHostConst.ExitCouldNotCreatePluginHost);
      }
    }

    private static Type LoadPluginHostType(string pluginHostTypeAssemblyName,
                                           string pluginHostTypeQualifiedName)
    {
      var pluginHostAssembly = Assembly.LoadFrom(pluginHostTypeAssemblyName);
      var exportedTypes = pluginHostAssembly.GetExportedTypes();

      // TODO: Check whether type inherits from PluginManager.Interop.PluginHost.PluginHostBase<>
      return exportedTypes.FirstOrDefault(t => t.FullName == pluginHostTypeQualifiedName);
    }

    /// <summary>
    ///   Creates the <see cref="AppDomain" /> that will host the plugin instance. The Assembly
    ///   Resolver for the plugin's and its dependencies' assemblies is instantiated in the plugin's
    ///   AppDomain itself. This avoids any unnecessary cross-AppDomain communication, and doesn't
    ///   require the plugin's AppDomain to load the PluginHost.exe assembly.
    /// </summary>
    /// <param name="packageRootFolder">
    ///   The root folder underneath which all packages and their
    ///   assemblies are located
    /// </param>
    /// <param name="pluginAndDependenciesAssembliesPath">
    ///   The file path to plugin's and its
    ///   dependencies' assemblies. Paths should be relative to <paramref name="packageRootFolder" />
    /// </param>
    /// <returns>The created <see cref="AppDomain" /></returns>
    private static void CreateAssemblyResolver(string packageRootFolder,
                                             string pluginAndDependenciesAssembliesPath)
    {
      var pluginManagerInteropAssemblyFilePath = FindAssemblyFilePath(
        false,
        null,
        PluginHostConst.PluginManagerInteropAssemblyName,
        packageRootFolder,
        pluginAndDependenciesAssembliesPath);

      if (string.IsNullOrWhiteSpace(pluginManagerInteropAssemblyFilePath))
      {
        Console.Error.WriteLine(
          $"Unable to find {PluginHostConst.PluginManagerInteropAssemblyName} assembly file path "
          + $"in assembly list {pluginAndDependenciesAssembliesPath}");

        throw new PluginHostException(PluginHostConst.ExitCouldNotFindInteropAssembly);
      }

      PluginAssemblyResolver.Initialize(packageRootFolder, pluginAndDependenciesAssembliesPath);
    }

    /// <summary>
    ///   If <paramref name="isDev" /> is <see langword="false" />, tries to find the
    ///   <paramref name="assemblyName" /> assembly's file path in
    ///   <paramref name="pluginAndDependenciesAssembliesPath" />. If <paramref name="isDev" /> is
    ///   <see langword="true" />, assumes the assembly file is located in
    ///   <paramref name="pluginHomeDir" />
    /// </summary>
    /// <param name="isDev">Whether the plugin is a development plugin</param>
    /// <param name="pluginHomeDir">The home directory of the plugin</param>
    /// <param name="assemblyName">The assembly name for which to find the assembly file path</param>
    /// <param name="packageRootFolder">
    ///   The root folder underneath which all packages and their
    ///   assemblies are located
    /// </param>
    /// <param name="pluginAndDependenciesAssembliesPath">
    ///   The file path to plugin's and its
    ///   dependencies' assemblies. Paths should be relative to <paramref name="packageRootFolder" />
    /// </param>
    /// <returns>The file path to the assembly named <paramref name="assemblyName"/></returns>
    private static string FindAssemblyFilePath(
      bool   isDev,
      string pluginHomeDir,
      string assemblyName,
      string packageRootFolder,
      string pluginAndDependenciesAssembliesPath)
    {
      if (isDev)
        return Path.Combine(pluginHomeDir, assemblyName + ".dll");

      var pluginManagerAssemblyFileName = assemblyName + ".dll";
      var pluginAndDependenciesAssembliesPathArr = pluginAndDependenciesAssembliesPath.Split(
        new[] { PluginHostConst.PluginAndDependenciesAssembliesSeparator },
        StringSplitOptions.RemoveEmptyEntries);

      var pluginManagerAssembly = pluginAndDependenciesAssembliesPathArr.FirstOrDefault(
        fp => fp.EndsWith(pluginManagerAssemblyFileName));

      if (pluginManagerAssembly == null)
        return null;

      return packageRootFolder + pluginManagerAssembly;
    }

    #endregion
  }
}
