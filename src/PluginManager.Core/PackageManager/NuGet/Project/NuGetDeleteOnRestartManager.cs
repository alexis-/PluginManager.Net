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
// Modified On:  2020/03/06 14:11
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anotar.Custom;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using PluginManager.Sys.RestartManager;

namespace PluginManager.PackageManager.NuGet.Project
{
  /// <summary>Exposes methods which mark partially deleted packages and deletes them.</summary>
  internal class NuGetDeleteOnRestartManager<TMeta> : IDeleteOnRestartManager
  {
    #region Properties & Fields - Non-Public

    private readonly NuGetInstalledPluginRepository<TMeta> _pluginRepo;

    #endregion




    #region Constructors

    public NuGetDeleteOnRestartManager(NuGetInstalledPluginRepository<TMeta> pluginRepo)
    {
      _pluginRepo = pluginRepo;
    }

    #endregion




    #region Methods Impl

    /// <summary>
    ///   Gets the list of package directories that are still need to be deleted in the local
    ///   package repository.
    /// </summary>
    public IReadOnlyList<string> GetPackageDirectoriesMarkedForDeletion()
    {
      return _pluginRepo.GetPackageDirectoriesMarkedForDeletion();
    }

    /// <summary>
    ///   Checks for any package directories that are pending to be deleted and raises the
    ///   <see cref="E:NuGet.PackageManagement.IDeleteOnRestartManager.PackagesMarkedForDeletionFound" />
    ///   event.
    /// </summary>
    public void CheckAndRaisePackageDirectoriesMarkedForDeletion()
    {
      var directories = GetPackageDirectoriesMarkedForDeletion();

      if (directories != null && directories.Any())
        PackagesMarkedForDeletionFound?.Invoke(this, new PackagesMarkedForDeletionEventArgs(directories));
    }

    /// <summary>
    ///   Marks package directory for future removal if it was not fully deleted during the
    ///   normal uninstall process if the directory does not contain any added or modified files.
    /// </summary>
    public void MarkPackageDirectoryForDeletion(PackageIdentity      package,
                                                string               packageDirectory,
                                                INuGetProjectContext projectContext)
    {
      _pluginRepo.AddPackageDirectoryForDeletion(packageDirectory);
    }

    /// <summary>
    ///   Attempts to remove marked package directories that were unable to be fully deleted
    ///   during the original uninstall.
    /// </summary>
    public async Task DeleteMarkedPackageDirectoriesAsync(INuGetProjectContext projectContext)
    {
      foreach (var directory in GetPackageDirectoriesMarkedForDeletion())
      {
        string    failedPath  = null;
        bool      isFailedDir = false;
        Exception failedEx    = null;

        if (await Task.Run(() => DeleteDirectoryRecursive(directory, out failedPath, out isFailedDir, out failedEx)))
          continue;

        if (failedPath != null)
        {
          var lockerMsg = string.Empty;
          var lockers   = RestartManager.FindLockerProcesses(failedPath);

          if (lockers.Length > 0)
            lockerMsg =
              $"\nThe {(isFailedDir ? "folder" : "file")} which threw an exception is locked by processes: {string.Join("; ", lockers.Select(l => l.strAppName))}";

          LogTo.Warning(
            $"Failed to deleted package directory marked for deletion: '{directory}'.\nDeleting {failedPath} threw an exception (see below).{lockerMsg}\n\n{failedEx}");
        }

        LogTo.Warning($"Failed to delete package directory marked for deletion: '{directory}'.");
      }
    }

    #endregion




    #region Methods

    private bool DeleteDirectoryRecursive(string folder, out string failedPath, out bool failedIsDir, out Exception failedEx)
    {
      failedPath  = null;
      failedIsDir = false;
      failedEx    = null;

      try
      {
        if (Directory.Exists(folder) == false)
          return true;

        var folderInfo = Directory.CreateDirectory(folder);

        return DeleteDirectoryRecursive(folderInfo, out failedPath, out failedIsDir, out failedEx);
      }
      catch (Exception ex)
      {
        failedEx = ex;

        return false;
      }
    }

    private bool DeleteDirectoryRecursive(DirectoryInfo folderInfo, out string failedPath, out bool failedIsDir, out Exception failedEx)
    {
      failedPath  = null;
      failedIsDir = false;
      failedEx    = null;

      try
      {
        foreach (var fileInfo in folderInfo.EnumerateFiles())
        {
          failedPath = fileInfo.FullName;

          fileInfo.Delete();
        }

        failedIsDir = true;

        foreach (var subFolderInfo in folderInfo.EnumerateDirectories())
        {
          if (DeleteDirectoryRecursive(subFolderInfo.FullName, out failedPath, out failedIsDir, out failedEx) == false)
            return false;

          failedPath = subFolderInfo.FullName;
        }

        return true;
      }
      catch (Exception ex)
      {
        failedEx = ex;

        return false;
      }
    }

    #endregion




    #region Events

    /// <summary>
    ///   Occurs when it is detected that the one or more packages are marked for deletion in
    ///   the current solution.
    /// </summary>
    public event EventHandler<PackagesMarkedForDeletionEventArgs> PackagesMarkedForDeletionFound;

    #endregion
  }
}
