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
// Modified On:  2020/02/24 18:02
// Modified By:  Alexis

#endregion




using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Anotar.Custom;
using Extensions.System.IO;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using PluginManager.PackageManager.NuGet;

namespace PluginManager.Extensions
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  internal static class NuGetPackageEx
  {
    #region Methods

    /// <summary>Returns the package directory path if it exists, null otherwise</summary>
    public static DirectoryPath GetPackageDirectoryPath(this NuGetPackage pkg, FolderNuGetProject project)
    {
      return project.GetInstalledPath(pkg.Identity);
    }

    /// <summary>Returns the .nupkg file path if it exists, null otherwise</summary>
    public static FilePath GetPackageFilePath(this NuGetPackage pkg, FolderNuGetProject project)
    {
      return project.GetInstalledPackageFilePath(pkg.Identity);
    }

    /// <summary>Verifies that the package exist locally</summary>
    public static bool PackageExists(this NuGetPackage pkg, NuGetPackageManager packageManager)
    {
      return packageManager.PackageExistsInPackagesFolder(pkg.Identity);
    }

    /// <summary>Returns the referenced .dll/.exe file paths</summary>
    public static IEnumerable<FilePath> GetReferencedAssembliesFilePaths(
      this NuGetPackage  pkg,
      FolderNuGetProject project,
      NuGetFramework     targetFramework)
    {
      if (pkg == null)
        yield break;

      var pkgPath       = pkg.GetPackageDirectoryPath(project);
      var archiveReader = pkg.GetArchiveReader(project);

      var referenceItems = archiveReader.GetReferenceItems().ToList();
      var referenceGroup = SelectFrameworkMostCompatibleGroup(targetFramework, referenceItems);

      if (referenceGroup != null)
      {
        LogTo.Trace(
          $"Found compatible reference group {referenceGroup.TargetFramework.DotNetFrameworkName} for package {pkg.Identity}");

        foreach (FilePath assemblyPath in referenceGroup.Items
                                                        .Select(x => new FilePath(x))
                                                        .Where(x => x.Extension == ".dll" || x.Extension == ".exe")
                                                        .Select(pkgPath.CombineFile))
        {
          LogTo.Trace($"Found NuGet reference {assemblyPath} from package {pkg.Identity}");

          yield return assemblyPath;
        }
      }
      else if (referenceItems.Count == 0)
      {
        // Only show a verbose message if there were no reference items (I.e., it's probably a content-only package or a metapackage and not a mismatch)
        LogTo.Trace($"Could not find any reference items in package {pkg.Identity}");
      }
      else
      {
        LogTo.Trace(
          $"Could not find compatible reference group for package {pkg.Identity} (found {string.Join((string)",", (IEnumerable<string>)referenceItems.Select(x => x.TargetFramework.DotNetFrameworkName))})");
      }
    }

    public static IEnumerable<DirectoryPath> GetContentDirectoryPath(
      this NuGetPackage  pkg,
      FolderNuGetProject project,
      NuGetFramework     targetFramework)
    {
      if (pkg == null)
        yield break;

      var pkgPath = pkg.GetPackageDirectoryPath(project);

      if (pkgPath == null)
        yield break;

      var archiveReader = pkg.GetArchiveReader(project);

      if (archiveReader == null)
        yield break;

      FrameworkSpecificGroup contentGroup = SelectFrameworkMostCompatibleGroup(targetFramework, archiveReader.GetContentItems().ToList());

      if (contentGroup != null)
        foreach (DirectoryPath contentPath in contentGroup.Items.Select(x => new FilePath(x).Segments[0])
                                                          .Distinct()
                                                          .Select(x => pkgPath.Combine(x)))
        {
          LogTo.Trace(
            $"Found content path {contentPath} from compatible content group {contentGroup.TargetFramework.DotNetFrameworkName} from package {pkg.Identity}");

          yield return contentPath;
        }
    }

    private static PackageArchiveReader GetArchiveReader(this NuGetPackage pkg, FolderNuGetProject project)
    {
      var pkgPath = pkg.GetPackageFilePath(project)?.FullPath;

      if (pkgPath == null)
        return null;

      return new PackageArchiveReader(pkgPath, null, null);
    }

    /// The following method is originally from the internal MSBuildNuGetProjectSystemUtility class
    private static FrameworkSpecificGroup SelectFrameworkMostCompatibleGroup(NuGetFramework               projectTargetFramework,
                                                                             List<FrameworkSpecificGroup> itemGroups)
    {
      FrameworkReducer reducer = new FrameworkReducer();
      NuGetFramework mostCompatibleFramework
        = reducer.GetNearest(projectTargetFramework, itemGroups.Select(i => i.TargetFramework));
      if (mostCompatibleFramework != null)
      {
        FrameworkSpecificGroup mostCompatibleGroup
          = itemGroups.FirstOrDefault(i => i.TargetFramework.Equals(mostCompatibleFramework));

        if (IsValidFrameworkGroup(mostCompatibleGroup))
          return mostCompatibleGroup;
      }

      return null;
    }

    /// The following method is originally from the internal MSBuildNuGetProjectSystemUtility class
    private static bool IsValidFrameworkGroup(FrameworkSpecificGroup frameworkSpecificGroup)
    {
      if (frameworkSpecificGroup != null)
        return frameworkSpecificGroup.HasEmptyFolder
          || frameworkSpecificGroup.Items.Any<string>()
          || !frameworkSpecificGroup.TargetFramework.Equals(NuGetFramework.AnyFramework);

      return false;
    }

    #endregion
  }
}
