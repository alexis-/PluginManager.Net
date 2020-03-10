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
// Modified On:  2020/03/06 16:42
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Windows;

namespace PluginHost
{
  public static class PluginLoader
  {
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
    /// <returns>The instantiated plugin cast to <see cref="IDisposable" /></returns>
    public static IDisposable Create(string  packageRootFolder,
                                     string  pluginAndDependenciesAssembliesPath,
                                     string  pluginHostTypeAssemblyName,
                                     string  pluginHostTypeQualifiedName,
                                     string  pluginPackageName,
                                     string  pluginHomeDir,
                                     Guid    sessionGuid,
                                     string  mgrChannelName,
                                     Process mgrProcess,
                                     bool    isDev)
    {
      var appDomain = CreateAppDomain(
        pluginPackageName,
        packageRootFolder,
        pluginAndDependenciesAssembliesPath,
        pluginHomeDir,
        isDev);

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
        Application.Current.Shutdown(PluginHostConst.ExitCouldNotFindPluginAssembly);
      }

      return (IDisposable)appDomain.CreateInstanceAndUnwrap(
        pluginHostTypeAssemblyName, pluginHostTypeQualifiedName,
        false,
        BindingFlags.Public | BindingFlags.Instance,
        null,
        new object[]
        {
          pluginEntryAssemblyFilePath,
          sessionGuid,
          mgrChannelName,
          mgrProcess,
          isDev
        },
        null,
        null
      );
    }

    /// <summary>
    ///   Creates the <see cref="AppDomain" /> that will host the plugin instance. The Assembly
    ///   Resolver for the plugin's and its dependencies' assemblies is instantiated in the plugin's
    ///   AppDomain itself. This avoids any unnecessary cross-AppDomain communication, and doesn't
    ///   require the plugin's AppDomain to load the PluginHost.exe assembly.
    /// </summary>
    /// <param name="packageName">The plugin's package name</param>
    /// <param name="packageRootFolder">
    ///   The root folder underneath which all packages and their
    ///   assemblies are located
    /// </param>
    /// <param name="pluginAndDependenciesAssembliesPath">
    ///   The file path to plugin's and its
    ///   dependencies' assemblies. Paths should be relative to <paramref name="packageRootFolder" />
    /// </param>
    /// <param name="pluginHomeDir">The home directory of the plugin</param>
    /// <param name="isDev">Whether the plugin is a development plugin</param>
    /// <returns>The created <see cref="AppDomain" /></returns>
    private static AppDomain CreateAppDomain(string packageName,
                                             string packageRootFolder,
                                             string pluginAndDependenciesAssembliesPath,
                                             string pluginHomeDir,
                                             bool   isDev)
    {
      var appDomainSetup = new AppDomainSetup
      {
        ApplicationBase = pluginHomeDir,
        PrivateBinPath  = GetAppDomainBinPath(pluginHomeDir),
      };

      var permissions = GetAppDomainPermissions(packageName, pluginHomeDir, isDev);

      var appDomain = AppDomain.CreateDomain(
        PluginHostConst.AppDomainName,
        AppDomain.CurrentDomain.Evidence,
        appDomainSetup,
        permissions
      );

      // Instantiate the assembly resolver in the child's App Domain
      if (isDev == false)
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
          Application.Current.Shutdown(PluginHostConst.ExitCouldNotFindInteropAssembly);
          return null; // Avoids intellisense null reference confusion
        }

        appDomain.CreateInstanceFrom(
          pluginManagerInteropAssemblyFilePath,
          PluginHostConst.PluginManagerInteropAssemblyResolverType,
          false,
          BindingFlags.Public | BindingFlags.Instance,
          null,
          new object[]
          {
            packageRootFolder,
            pluginAndDependenciesAssembliesPath
          },
          null,
          null
        );
      }

      return appDomain;
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
    /// <returns>PluginManager.Interop assembly's file path</returns>
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

    /// <summary>Creates the AppDomain's Private path</summary>
    /// <param name="homeDir"></param>
    /// <returns></returns>
    private static string GetAppDomainBinPath(string homeDir)
    {
      if (homeDir.EndsWith("\\") == false)
        homeDir += "\\";

      List<string> ret = new List<string> { homeDir };

      //if (string.IsNullOrWhiteSpace(AppDomain.CurrentDomain.SetupInformation.PrivateBinPath) == false)
      //  ret.AddRange(AppDomain.CurrentDomain.SetupInformation.PrivateBinPath.Split(';'));

      //ret.Add(AppDomain.CurrentDomain.SetupInformation.ApplicationBase);

      return string.Join(";", ret.Select(p => p.Replace('/', '\\')));
    }

    // ReSharper disable UnusedParameter.Local
    /// <summary>Set up AppDomain's permissions (TODO)</summary>
    /// <param name="packageName"></param>
    /// <param name="homeDir"></param>
    /// <param name="isDev"></param>
    /// <returns></returns>
    private static PermissionSet GetAppDomainPermissions(string packageName,
                                                         string homeDir,
                                                         bool   isDev)
    {
      // ReSharper restore UnusedParameter.Local

      // TODO: Switch back to PermissionState.None
      var permissions = new PermissionSet(PermissionState.Unrestricted);

#if false
      //permissions.SetPermission(new EnvironmentPermission(PermissionState.Unrestricted));
      //permissions.SetPermission(new UIPermission(PermissionState.Unrestricted));
      //permissions.SetPermission(new FileDialogPermission(PermissionState.Unrestricted));
      //permissions.SetPermission(new MediaPermission(PermissionState.Unrestricted));
      //permissions.SetPermission(new ReflectionPermission(PermissionState.Unrestricted));
      //permissions.SetPermission(new GacIdentityPermission(PermissionState.Unrestricted));

      //System.Security.Permissions.
      //permissions.SetPermission(
      //  new SecurityPermission(SecurityPermissionFlag.AllFlags));
      //SecurityPermissionFlag.Execution | SecurityPermissionFlag.UnmanagedCode | SecurityPermissionFlag.BindingRedirects
      //| SecurityPermissionFlag.Assertion | SecurityPermissionFlag.RemotingConfiguration | SecurityPermissionFlag.ControlThread));

      //
      // IO
      permissions.RemovePermission(typeof(FileIOPermission));

      // Common windows locations
      permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess,
                                                     Path.GetTempPath()));
      permissions.AddPermission(new FileIOPermission(
                                  FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read,
                                  Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles))
      );
      permissions.AddPermission(new FileIOPermission(
                                  FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read,
                                  Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86))
      );

      // Plugin packages
      if (isDev == false)
        permissions.AddPermission(new FileIOPermission(
                                    FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read,
                                    SMAFileSystem.PluginPackageDir.FullPath));

      // Plugin config
      permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess,
                                                     SMAFileSystem.ConfigDir.Combine(packageName).FullPath));

      // Shared config
      permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess,
                                                     SMAFileSystem.SharedConfigDir.FullPath));

      // Data
      permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess,
                                                     SMAFileSystem.DataDir.Combine(packageName).FullPath));

      // Home
      permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess,
                                                     homeDir)); //AppDomain.CurrentDomain.BaseDirectory));
#endif

      return permissions;
    }

    #endregion
  }
}
