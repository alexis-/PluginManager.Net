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
// Modified On:  2020/03/10 00:50
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anotar.Custom;
using Extensions.System.IO;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using PluginManager.Extensions;
using PluginManager.PackageManager.Models;

namespace PluginManager.PackageManager.NuGet.Project
{
  /// <summary>This primarily exists to intercept package installations and store their paths</summary>
  internal class NuGetPluginProject<TMeta> : FolderNuGetProject
  {
    #region Properties & Fields - Non-Public

    private readonly NuGetFramework                                       _currentFramework;
    private readonly Func<NuGetPluginProject<TMeta>, NuGetPackageManager> _packageManagerCreator;
    private readonly Func<PackageIdentity, bool>                          _canUninstallPackageFunc;
    private readonly DirectoryPath                                        _pluginHomeDirPath;

    private bool _isInstalled;

    #endregion




    #region Constructors

    public NuGetPluginProject(Func<NuGetPluginProject<TMeta>, NuGetPackageManager> packageManagerCreator,
                              Func<PackageIdentity, bool>                          canUninstallPackageFunc,
                              NuGetFramework                                       currentFramework,
                              DirectoryPath                                        packageDirPath,
                              DirectoryPath                                        pluginHomeDirPath,
                              LocalPluginPackage<TMeta>                            plugin,
                              bool                                                 isInstalled)
      : base(packageDirPath.FullPath)
    {
      Plugin                   = plugin;
      _packageManagerCreator   = packageManagerCreator;
      _canUninstallPackageFunc = canUninstallPackageFunc;
      _currentFramework        = currentFramework;
      _isInstalled             = isInstalled;
      _pluginHomeDirPath       = pluginHomeDirPath.Combine(Plugin.Id);

      InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, plugin.Id);
    }

    #endregion




    #region Properties & Fields - Public

    public LocalPluginPackage<TMeta> Plugin { get; }

    #endregion




    #region Methods Impl

    /// <summary>
    ///   This gets called for every package install, including dependencies, and is our only
    ///   chance to handle plugins' dependencies PackageIdentity instances If the package is already
    ///   installed, returns false.
    /// </summary>
    /// <param name="packageIdentity"></param>
    /// <param name="downloadResourceResult"></param>
    /// <param name="nuGetProjectContext"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public override Task<bool> InstallPackageAsync(
      PackageIdentity        packageIdentity,
      DownloadResourceResult downloadResourceResult,
      INuGetProjectContext   nuGetProjectContext,
      CancellationToken      token)
    {
      LogTo.Information(
        $"Installing plugin or dependency {packageIdentity.Id} {(packageIdentity.HasVersion ? packageIdentity.Version.ToNormalizedString() : string.Empty)}");

      if (packageIdentity.Id != Plugin.Identity.Id)
      {
        var dependency = Plugin.AddDependency(packageIdentity);

        foreach (var contentDir in dependency.GetContentDirectoryPath(this, _currentFramework))
          contentDir.CopyTo(_pluginHomeDirPath);
      }
      else
      {
        Plugin.Version = packageIdentity.Version.ToNormalizedString();
      }

      return base.InstallPackageAsync(packageIdentity, downloadResourceResult, nuGetProjectContext, token);
    }

    public override Task<bool> UninstallPackageAsync(PackageIdentity      packageIdentity,
                                                     INuGetProjectContext nuGetProjectContext,
                                                     CancellationToken    token)
    {
      // ReSharper disable PossibleMultipleEnumeration

      if (packageIdentity.Id != Plugin.Identity.Id)
      {
        if (_canUninstallPackageFunc(packageIdentity) == false)
        {
          LogTo.Information($"Dependency {packageIdentity.Id} is required by other plugins and won't be uninstalled.");
          return Task.FromResult(false);
        }

        LogTo.Information(
          $"Uninstalling dependency {packageIdentity.Id} {(packageIdentity.HasVersion ? packageIdentity.Version.ToNormalizedString() : string.Empty)}");

        var dependency = Plugin.RemoveDependency(packageIdentity);

        foreach (var contentDir in dependency.GetContentDirectoryPath(this, _currentFramework))
        {
          var filesAndDirs = contentDir.ListAllFilesAndDirectories();

          foreach (var filePath in filesAndDirs.Where(fod => fod is FilePath)
                                               .Cast<FilePath>())
            try
            {
              File.Delete(filePath.FullPath.Replace(contentDir.FullPath, Plugin.HomeDir.FullPath));
            }
            catch (IOException ioException)
            {
              LogTo.Warning(ioException, $"Error while cleaning a content file from uninstalled plugin {filePath.FullPath}");
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
              LogTo.Warning(unauthorizedAccessException,
                            $"Error while cleaning a content file from uninstalled plugin {filePath.FullPath}");
            }
            catch (Exception ex)
            {
              LogTo.Error(ex, $"Error while cleaning a content file from uninstalled plugin {filePath.FullPath}");
            }

          foreach (var dirPath in filesAndDirs.Where(fod => fod is DirectoryPath)
                                              .Cast<DirectoryPath>()
                                              .Select(d => d.Collapse())
                                              .OrderByDescending(d => d.FullPath.Length))
            try
            {
              if (Directory.GetFiles(dirPath.FullPath).Length > 0)
                continue;

              dirPath.Delete();
            }
            catch (IOException ioException)
            {
              LogTo.Warning(ioException, $"Error while cleaning a content directory from uninstalled plugin {dirPath.FullPath}");
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
              LogTo.Warning(unauthorizedAccessException,
                            $"Error while cleaning a content directory from uninstalled plugin {dirPath.FullPath}");
            }
            catch (Exception ex)
            {
              LogTo.Error(ex, $"Error while cleaning a content directory from uninstalled plugin {dirPath.FullPath}");
            }
        }
      }

      else
        LogTo.Information(
          $"Uninstalling plugin {packageIdentity.Id} {(packageIdentity.HasVersion ? packageIdentity.Version.ToNormalizedString() : string.Empty)}");

      return base.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);
      // ReSharper restore PossibleMultipleEnumeration
    }

    public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
    {
      var packages = _isInstalled
        ? Plugin.PluginAndDependencies
        : Plugin.Dependencies.Values;

      return Task.FromResult(
        packages.Select(
          p => new PackageReference(
            p.Identity,
            _currentFramework,
            true,
            false,
            false))
      );
    }

    #endregion




    #region Methods

    public async Task InstallPluginAsync(
      INuGetProjectContext          projectContext,
      IEnumerable<SourceRepository> sourceRepositories,
      bool                          allowPrereleaseVersions = false,
      CancellationToken             cancellationToken       = default)
    {
      if (_pluginHomeDirPath.Exists())
        _pluginHomeDirPath.Delete();

      _pluginHomeDirPath.Create();

      ResolutionContext resolutionContext = new ResolutionContext(
        DependencyBehavior.Lowest, allowPrereleaseVersions, false, VersionConstraints.None);

      await Task.Run(async () =>
      {
        await CreatePackageManager().InstallPackageAsync(
          this,
          Plugin.Identity,
          resolutionContext,
          projectContext,
          sourceRepositories,
          Array.Empty<SourceRepository>(),
          cancellationToken);
      }).ConfigureAwait(false);

      foreach (var contentDir in Plugin.GetContentDirectoryPath(this, _currentFramework))
        contentDir.CopyTo(_pluginHomeDirPath);

      _isInstalled = true;
    }

    public async Task UninstallPluginAsync(
      INuGetProjectContext projectContext,
      bool                 removeDependencies = true,
      bool                 forceRemove        = false,
      CancellationToken    cancellationToken  = default)
    {
      var uninstallContext = new UninstallationContext(removeDependencies, forceRemove);

      await Task.Run(async () =>
      {
        await CreatePackageManager().UninstallPackageAsync(
          this,
          Plugin.Id,
          uninstallContext,
          projectContext,
          cancellationToken);
      }).ConfigureAwait(false);

      if (_pluginHomeDirPath.Exists())
        _pluginHomeDirPath.Delete();
    }

    public async Task UpdatePluginAsync(
      INuGetProjectContext          projectContext,
      IEnumerable<SourceRepository> sourceRepositories,
      NuGetVersion                  version                 = null,
      bool                          allowPrereleaseVersions = false,
      CancellationToken             cancellationToken       = default)
    {
      ResolutionContext resolutionContext = new ResolutionContext(
        DependencyBehavior.Lowest, allowPrereleaseVersions, false, VersionConstraints.None);

      await Task.Run(async () =>
      {
        var pm = CreatePackageManager();

        IEnumerable<NuGetProjectAction> actions;

        if (version == null)
          actions = await pm.PreviewUpdatePackagesAsync(
            new List<NuGetProject> { this },
            resolutionContext,
            projectContext,
            sourceRepositories,
            Array.Empty<SourceRepository>(),
            cancellationToken);

        else
          actions = await pm.PreviewUpdatePackagesAsync(
            new PackageIdentity(Plugin.Id, version),
            new List<NuGetProject> { this },
            resolutionContext,
            projectContext,
            sourceRepositories,
            Array.Empty<SourceRepository>(),
            cancellationToken);

        using (var sourceCacheContext = new SourceCacheContext())
          await pm.ExecuteNuGetProjectActionsAsync(this, actions, projectContext, sourceCacheContext, cancellationToken);
      }).ConfigureAwait(false);
      
      foreach (var contentDir in Plugin.GetContentDirectoryPath(this, _currentFramework))
        contentDir.CopyTo(_pluginHomeDirPath);
    }

    public NuGetPackageManager CreatePackageManager()
    {
      return _packageManagerCreator(this);
    }

    #endregion
  }
}
