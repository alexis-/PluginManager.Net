﻿#region License & Metadata

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
// Modified On:  2020/02/25 01:04
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;

namespace PluginHost
{
  public static class PluginLoader
  {
    #region Methods

    public static IDisposable Create(string  pluginHostTypeAssemblyName,
                                     string  pluginHostTypeQualifiedName,
                                     string  pluginPackageName,
                                     string  pluginHomeDir,
                                     Guid    sessionGuid,
                                     string  mgrChannelName,
                                     Process mgrProcess,
                                     bool    isDev)
    {
      var appDomain = CreateAppDomain(pluginPackageName, pluginHomeDir, isDev);

      return (IDisposable)appDomain.CreateInstanceAndUnwrap(
        pluginHostTypeAssemblyName, pluginHostTypeQualifiedName,
        false,
        BindingFlags.Public | BindingFlags.Instance,
        null,
        new object[] { pluginPackageName, sessionGuid, mgrChannelName, mgrProcess, isDev },
        null,
        null
      );
    }

    private static AppDomain CreateAppDomain(string packageName,
                                             string homeDir,
                                             bool   isDev)
    {
      var appDomainSetup = new AppDomainSetup
      {
        ApplicationBase = homeDir,
        PrivateBinPath  = GetAppDomainBinPath(homeDir),
      };

      var permissions = GetAppDomainPermissions(packageName, homeDir, isDev);

      var appDomain = AppDomain.CreateDomain(
        PluginHostConst.AppDomainName,
        AppDomain.CurrentDomain.Evidence,
        appDomainSetup,
        permissions
      );

      return appDomain;
    }

    private static string GetAppDomainBinPath(string homeDir)
    {
      List<string> ret = new List<string> { homeDir + '\\' };

      //if (string.IsNullOrWhiteSpace(AppDomain.CurrentDomain.SetupInformation.PrivateBinPath) == false)
      //  ret.AddRange(AppDomain.CurrentDomain.SetupInformation.PrivateBinPath.Split(';'));

      //ret.Add(AppDomain.CurrentDomain.SetupInformation.ApplicationBase);

      return string.Join(";", ret.Select(p => p.Replace('/', '\\')));
    }

    // ReSharper disable UnusedParameter.Local
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
