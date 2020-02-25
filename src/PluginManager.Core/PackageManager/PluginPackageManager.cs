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
// Modified On:  2020/02/24 18:03
// Modified By:  Alexis

#endregion




using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Anotar.Custom;
using Extensions.System.IO;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using PluginManager.Contracts;
using PluginManager.Extensions;
using PluginManager.Logger;
using PluginManager.PackageManager.Models;
using PluginManager.PackageManager.NuGet;
using PluginManager.PackageManager.NuGet.Project;
using SourceRepositoryProvider = PluginManager.PackageManager.NuGet.SourceRepositoryProvider;

namespace PluginManager.PackageManager
{
  /// <summary>A wrapper around <see cref="NuGetPackageManager" /> to simplify package management.</summary>
  public class PluginPackageManager<TMeta> : IReadOnlyCollection<PluginPackage<TMeta>>
  {
    #region Properties & Fields - Non-Public

    private readonly IPluginRepositoryService<TMeta> _repoService;

    private readonly NuGetFramework                        _currentFramework;
    private readonly PluginManagerLogger                   _logger = new PluginManagerLogger();
    private readonly NuGetInstalledPluginRepository<TMeta> _pluginRepo;
    private readonly NuGetPluginSolution<TMeta>            _solution;
    private readonly SourceRepositoryProvider              _sourceRepositories;

    #endregion




    #region Constructors

    public PluginPackageManager(DirectoryPath                             pluginDirPath,
                                DirectoryPath                             pluginHomeDirPath,
                                DirectoryPath                             packageDirPath,
                                FilePath                                  configFilePath,
                                IPluginRepositoryService<TMeta>           repoService,
                                Func<ISettings, SourceRepositoryProvider> providerCreator = null)
    {
      pluginDirPath  = pluginDirPath.Collapse();
      packageDirPath = packageDirPath.Collapse();

      if (pluginDirPath.EnsureExists() == false)
        throw new ArgumentException($"Root path {pluginDirPath.FullPath} doesn't exist and couldn't be created.");

      if (packageDirPath.EnsureExists() == false)
        throw new ArgumentException($"Package path {packageDirPath.FullPath} doesn't exist and couldn't be created.");

      if (configFilePath.Root.Exists() == false)
        throw new ArgumentException($"Config's root directory {configFilePath.Root.FullPath} doesn't exist and couldn't be created.");

      var packageCacheTask = NuGetInstalledPluginRepository<TMeta>.LoadAsync(configFilePath, pluginHomeDirPath);
      var settings         = Settings.LoadDefaultSettings(packageDirPath.FullPath, null, new NuGetMachineWideSettings());

      _currentFramework   = GetCurrentFramework();
      _sourceRepositories = providerCreator?.Invoke(settings) ?? new SourceRepositoryProvider(settings);
      _pluginRepo         = packageCacheTask.Result;
      _solution = new NuGetPluginSolution<TMeta>(
        pluginDirPath, pluginHomeDirPath, packageDirPath,
        _pluginRepo,
        _sourceRepositories,
        settings,
        _currentFramework
      );

      _repoService = repoService;
    }

    #endregion




    #region Properties Impl - Public

    /// <inheritdoc />
    public int Count { get; } // TODO: Sync PluginPackageManager with all available packages

    #endregion




    #region Methods Impl

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator(); // TODO: Sync PluginPackageManager with all available packages
    }


    /// <inheritdoc />
    public IEnumerator<PluginPackage<TMeta>> GetEnumerator()
    {
      throw new NotImplementedException(); // TODO: Sync PluginPackageManager with all available packages
    }

    #endregion




    #region Methods

    /// <summary>
    ///   Adds the specified package source. Sources added this way will be searched before any
    ///   global sources.
    /// </summary>
    /// <param name="repository">The package source to add.</param>
    public SourceRepository AddRepository(string repository) => _sourceRepositories.CreateRepository(repository);

    /// <summary>Gets all installed versions of package <paramref name="packageId" />, if any.</summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="verify">
    ///   Whether to verify if each version, and its dependencies, are correctly
    ///   installed.
    /// </param>
    public IEnumerable<NuGetVersion> FindInstalledPluginVersions(string packageId,
                                                                 bool   verify = false)
    {
      var pkgs = _pluginRepo.FindPackageById(packageId);

      if (verify)
        pkgs = pkgs.Where(p => _pluginRepo.IsPluginInstalled(p.Identity, _solution));

      return pkgs.Select(p => p.Identity.Version);
    }

    public LocalPluginPackage<TMeta> FindInstalledPluginById(string packageId) => _pluginRepo.FindPluginById(packageId);

    public LocalPluginPackage<TMeta> GetInstalledPlugin(PackageIdentity identity) => _pluginRepo[identity];

    public IEnumerable<LocalPluginPackage<TMeta>> GetInstalledPlugins() => _pluginRepo.Plugins;

    /// <summary>
    ///   Gets assemblies (.dll, .exe) file paths referenced in given
    ///   <paramref name="packageIdentity" /> and its dependencies , for framework
    ///   <paramref name="targetFramework" />
    /// </summary>
    /// <param name="packageIdentity">Package to look for</param>
    /// <param name="dependenciesAssemblies"></param>
    /// <param name="targetFramework">
    ///   Target framework to match, or current assembly's framework if
    ///   <see langword="null" />
    /// </param>
    /// <param name="pluginAssemblies"></param>
    public void GetInstalledPluginAssembliesFilePath(PackageIdentity           packageIdentity,
                                                     out IEnumerable<FilePath> pluginAssemblies,
                                                     out IEnumerable<FilePath> dependenciesAssemblies,
                                                     NuGetFramework            targetFramework = null)
    {
      var project = _solution.GetPluginProject((string)packageIdentity.Id);
      var plugin  = _pluginRepo[packageIdentity];

      if (project == null || plugin == null)
        throw new ArgumentException($"No such plugin {packageIdentity.Id} {packageIdentity.Version.ToNormalizedString()}");

      pluginAssemblies = plugin.GetReferencedAssembliesFilePaths(project, targetFramework ?? _currentFramework);
      dependenciesAssemblies =
        plugin.Dependencies.SelectMany(i => i.GetReferencedAssembliesFilePaths(project, targetFramework ?? _currentFramework));
    }

    public Task<bool> UninstallAsync(LocalPluginPackage<TMeta> pluginPkg,
                                     bool                      removeDependencies = true,
                                     bool                      forceRemove        = false,
                                     CancellationToken         cancellationToken  = default)
    {
      try
      {
        LogTo.Trace($"Uninstall requested for plugin {pluginPkg.Id}");

        return _solution.UninstallPluginAsync(
          pluginPkg.Id,
          removeDependencies,
          forceRemove,
          cancellationToken);
      }
      catch (Exception ex)
      {
        LogTo.Error(
          $"Unexpected exception while uninstalling packages: {(ex is AggregateException aggEx ? string.Join("; ", aggEx.InnerExceptions.Select(x => x.Message)) : ex.Message)}");

        return Task.FromResult(false);
      }
    }

    public async Task<bool> UpdateAsync()
    {
      await Task.Run(() => throw new NotImplementedException());

      return true;
    }

    public async Task<bool> InstallAsync(
      string            packageId,
      TMeta             metadata                = default,
      VersionRange      versionRange            = null,
      bool              allowPrereleaseVersions = false,
      NuGetFramework    framework               = null,
      CancellationToken cancellationToken       = default)
    {
      try
      {
        var version = (await SearchMatchingVersion(packageId, versionRange, framework, cancellationToken).ConfigureAwait(false)).Max();

        if (version == null)
          return false;

        LogTo.Trace($"Install requested for plugin {packageId} {version.ToNormalizedString()}");

        // Check if this package was already installed in a previous run
        PackageIdentity packageIdentity = new PackageIdentity(packageId, version);

        // If plugin exact version already exists, abort
        if (_pluginRepo.IsPluginInstalled(packageIdentity, _solution))
        {
          LogTo.Information("Already got plugin {packageId} {version.ToNormalizedString()}");

          return true;
        }

        // If plugin already exists in a different version, try to update it
        if (_pluginRepo.FindPluginById(packageId) != null)
        {
          LogTo.Information("Plugin already exist with a different version. Redirecting to UpdateAsync.");

          return await UpdateAsync();
        }

        // If plugin doesn't exist, go ahead and install it
        return await _solution.InstallPluginAsync(
          packageIdentity,
          metadata,
          allowPrereleaseVersions,
          cancellationToken);
      }
      catch (Exception ex)
      {
        LogTo.Error(
          $"Unexpected exception while installing packages: {(ex is AggregateException aggEx ? string.Join("; ", aggEx.InnerExceptions.Select(x => x.Message)) : ex.Message)}");

        throw;
      }
    }

    public Task<IEnumerable<PluginPackage<TMeta>>> Search(
      string            searchTerm,
      bool              enablePreRelease  = false,
      CancellationToken cancellationToken = default)
    {
      return Search(searchTerm, _sourceRepositories, enablePreRelease, cancellationToken);
    }

    public Task<IEnumerable<PluginPackage<TMeta>>> Search(
      string                    searchTerm,
      ISourceRepositoryProvider provider,
      bool                      enablePreRelease  = false,
      CancellationToken         cancellationToken = default)
    {
      return Search(searchTerm, provider.GetRepositories(), enablePreRelease, cancellationToken);
    }

    public async Task<IEnumerable<PluginPackage<TMeta>>> Search(
      string                        searchTerm,
      IEnumerable<SourceRepository> repositories,
      bool                          enablePreRelease  = false,
      CancellationToken             cancellationToken = default)
    {
      var tasks = repositories.Select(r => Search(searchTerm, r, enablePreRelease, cancellationToken));

      return (await Task.WhenAll(tasks)).SelectMany(a => a);
    }

    public async Task<List<PluginPackage<TMeta>>> Search(
      string            searchTerm,
      SourceRepository  sourceRepository,
      bool              enablePreRelease  = false,
      CancellationToken cancellationToken = default)
    {
      var searchPkgs   = await _repoService.ListPlugins();
      var pkgSearchRes = await sourceRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);

      // Match all online plugins with those listed in pluginMetadatas
      var onlinePkgs = await pkgSearchRes.SearchAsync(
        searchTerm,
        new SearchFilter(enablePreRelease),
        0, 500,
        _logger,
        cancellationToken);

      onlinePkgs = onlinePkgs.Where(ps => searchPkgs.ContainsKey(ps.Identity.Id)).ToList();

      // Match local packages with online ones
      var localPkgs = GetInstalledPlugins().ToDictionary<LocalPluginPackage<TMeta>, string>(k => k.Identity.Id);

      PluginPackage<TMeta> CreatePackage(IGrouping<string, IPackageSearchMetadata> gsr)
      {
        if (localPkgs.ContainsKey(gsr.Key))
        {
          var localPkg = localPkgs[gsr.Key];
          localPkg.SetOnlineVersions(gsr);

          return localPkg;
        }

        return new OnlinePluginPackage<TMeta>(gsr.Key, searchPkgs[gsr.Key], gsr);
      }

      return onlinePkgs.GroupBy(sr => sr.Identity.Id)
                       .Select(CreatePackage)
                       .ToList();
    }

    public Task<IEnumerable<NuGetVersion>> SearchMatchingVersion(
      string            packageId,
      VersionRange      versionRange      = null,
      NuGetFramework    framework         = null,
      CancellationToken cancellationToken = default)
    {
      return SearchMatchingVersion(_sourceRepositories, packageId, versionRange, framework, cancellationToken);
    }

    public Task<IEnumerable<NuGetVersion>> SearchMatchingVersion(
      ISourceRepositoryProvider provider,
      string                    packageId,
      VersionRange              versionRange      = null,
      NuGetFramework            framework         = null,
      CancellationToken         cancellationToken = default)
    {
      return SearchMatchingVersion((IEnumerable<SourceRepository>)provider.GetRepositories(), packageId, versionRange, framework,
                                   cancellationToken);
    }

    public async Task<IEnumerable<NuGetVersion>> SearchMatchingVersion(
      IEnumerable<SourceRepository> repositories,
      string                        packageId,
      VersionRange                  versionRange      = null,
      NuGetFramework                framework         = null,
      CancellationToken             cancellationToken = default)
    {
      var tasks = repositories.Select(r => SearchMatchingVersion(r, packageId, versionRange, framework, cancellationToken));

      return await Task.WhenAll(tasks);
    }

    public async Task<NuGetVersion> SearchMatchingVersion(
      SourceRepository  sourceRepository,
      string            packageId,
      VersionRange      versionRange      = null,
      NuGetFramework    framework         = null,
      CancellationToken cancellationToken = default)
    {
      try
      {
        var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>(cancellationToken);

        using (var sourceCacheContext = new SourceCacheContext())
        {
          var dependencyInfo =
            await dependencyInfoResource
              .ResolvePackages(packageId, framework ?? _currentFramework, sourceCacheContext, _logger, cancellationToken);

          return dependencyInfo
                 .Select(x => x.Version)
                 .Where(x => x != null && (versionRange == null || versionRange.Satisfies(x)))
                 .DefaultIfEmpty()
                 .Max();
        }
      }
      catch (Exception ex)
      {
        LogTo.Warning($"Could not get latest version for package {packageId} from source {sourceRepository}: {ex.Message}");

        return null;
      }
    }

    private NuGetFramework GetCurrentFramework()
    {
      Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
      string frameworkName = assembly.GetCustomAttributes(true)
                                     .OfType<System.Runtime.Versioning.TargetFrameworkAttribute>()
                                     .Select(x => x.FrameworkName)
                                     .FirstOrDefault();
      return frameworkName == null
        ? NuGetFramework.AnyFramework
        : NuGetFramework.ParseFrameworkName(frameworkName, new DefaultFrameworkNameProvider());
    }

    #endregion
  }
}
