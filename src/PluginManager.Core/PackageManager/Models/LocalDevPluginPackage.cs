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
// Modified On:  2020/03/04 14:40
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Diagnostics;
using Anotar.Custom;
using Extensions.System.IO;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using PluginManager.PackageManager.NuGet;

namespace PluginManager.PackageManager.Models
{
  /// <summary>
  ///   Represents a local development plugin. Unlike
  ///   <see cref="LocalPluginPackage{TMeta}" /> this plugin isn't in the form of a NuGet package
  ///   and all its files are located under a single folder
  /// </summary>
  /// <typeparam name="TMeta">The metadata to associate with each plugin</typeparam>
  public class LocalDevPluginPackage<TMeta> : LocalPluginPackage<TMeta>
  {
    #region Constructors

    protected LocalDevPluginPackage(PackageIdentity identity,
                                    DirectoryPath   devDir,
                                    TMeta           metadata)
      : base(identity, devDir, metadata) { }

    #endregion




    #region Properties Impl - Public
    
    /// <inheritdoc />
    public override bool HasPendingUpdates => false;

    #endregion




    #region Methods Impl

    /// <inheritdoc />
    public override NuGetPackage AddDependency(PackageIdentity packageIdentity)
    {
      throw new InvalidOperationException("Dev Plugin does not manage dependencies");
    }

    /// <inheritdoc />
    public override NuGetPackage RemoveDependency(PackageIdentity packageIdentity)
    {
      throw new InvalidOperationException("Dev Plugin does not manage dependencies");
    }

    /// <inheritdoc />
    public override IEnumerable<FilePath> GetPluginAndDependenciesAssembliesFilePaths(FolderNuGetProject project,
                                                                                      NuGetFramework     targetFramework)
    {
      throw new InvalidOperationException("Dev Plugin does not manage file paths");
    }

    /// <inheritdoc />
    public override IEnumerable<DirectoryPath> GetPluginAndDependenciesContentDirectoryPaths(FolderNuGetProject project,
                                                                                             NuGetFramework     targetFramework)
    {
      throw new InvalidOperationException("Dev Plugin does not manage file paths");
    }

    /// <inheritdoc />
    public override bool PackageAndDependenciesExist(NuGetPackageManager packageManager)
    {
      throw new InvalidOperationException("Dev Plugin does not manage files");
    }

    #endregion




    #region Methods

    public static LocalDevPluginPackage<TMeta> Create(string packageName, DirectoryPath devDir, CreateMetadata metadataFunc)
    {
      FilePath pluginFilePath = devDir.Combine(packageName).CombineFile(packageName + ".dll");

      if (pluginFilePath.Exists() == false)
      {
        LogTo.Warning($"Couldn't find development plugin dll {pluginFilePath.FullPath}. Skipping.");
        return null;
      }

      FileVersionInfo pluginVersionInfo = FileVersionInfo.GetVersionInfo(pluginFilePath.FullPath);

      if (pluginVersionInfo.ProductName != packageName)
      {
        LogTo.Warning(
          $"Development plugin Folder name {packageName} differs from Assembly name {pluginVersionInfo.ProductName}. Skipping.");
        return null;
      }

      packageName = pluginFilePath.FileNameWithoutExtension;

      return new LocalDevPluginPackage<TMeta>(
        new PackageIdentity(packageName, NuGetVersion.Parse(pluginVersionInfo.FileVersion)),
        devDir,
        metadataFunc(packageName, pluginVersionInfo)
      );
    }

    #endregion




    public delegate TMeta CreateMetadata(string packageName, FileVersionInfo fileVersionInfo);
  }
}
