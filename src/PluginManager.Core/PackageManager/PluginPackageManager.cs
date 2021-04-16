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
// Created On:   2020/03/29 00:20
// Modified On:  2020/04/24 02:35
// Modified By:  Alexis

#endregion




namespace PluginManager.PackageManager
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Threading;
  using System.Threading.Tasks;
  using Anotar.Custom;
  using Extensions;
  using global::Extensions.System.IO;
  using global::NuGet.Configuration;
  using global::NuGet.Frameworks;
  using global::NuGet.PackageManagement;
  using global::NuGet.Packaging.Core;
  using global::NuGet.Protocol;
  using global::NuGet.Protocol.Core.Types;
  using global::NuGet.Versioning;
  using Logger;
  using Models;
  using NuGet;
  using NuGet.Project;

  /// <summary>A wrapper around <see cref="NuGetPackageManager" /> to simplify package management.</summary>
  public sealed class PluginPackageManager<TMeta> : IReadOnlyCollection<PluginPackage<TMeta>>
  {
    #region Constructors

    private PluginPackageManager(DirectoryPath                         pluginDirPath,
                                 DirectoryPath                         pluginHomeDirPath,
                                 DirectoryPath                         packageDirPath,
                                 NuGetInstalledPluginRepository<TMeta> packageCache,
                                 Func<ISettings, NuGet.SourceRepositoryProvider> providerCreator =
                                   null)
    {
      var settings = Settings.LoadDefaultSettings(packageDirPath.FullPath, null, new NuGetMachineWideSettings());

      _currentFramework  = GetCurrentFramework();
      SourceRepositories = providerCreator?.Invoke(settings) ?? new NuGet.SourceRepositoryProvider(settings);
      PluginRepo         = packageCache;

      var localRepo = SourceRepositories.CreateRepository(
        new PackageSource(packageDirPath.FullPath, "Local", true),
        FeedType.FileSystemPackagesConfig);

      Solution = new NuGetPluginSolution<TMeta>(
        pluginDirPath, pluginHomeDirPath, packageDirPath,
        PluginRepo,
        SourceRepositories,
        settings,
        _currentFramework
      );
    }

    #endregion




    #region Properties & Fields - Public

    public readonly NuGetFramework                        _currentFramework;
    public readonly PluginManagerLogger                   _logger = new PluginManagerLogger();
    public          NuGetInstalledPluginRepository<TMeta> PluginRepo         { get; }
    public          NuGetPluginSolution<TMeta>            Solution           { get; }
    public          NuGet.SourceRepositoryProvider        SourceRepositories { get; }

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

    /// <summary>Instantiates a new package manager</summary>
    /// <param name="pluginDirPath"></param>
    /// <param name="pluginHomeDirPath"></param>
    /// <param name="packageDirPath"></param>
    /// <param name="configFilePath"></param>
    /// <param name="providerCreator"></param>
    /// <returns></returns>
    public static async Task<PluginPackageManager<TMeta>> Create(
      DirectoryPath                                   pluginDirPath,
      DirectoryPath                                   pluginHomeDirPath,
      DirectoryPath                                   packageDirPath,
      FilePath                                        configFilePath,
      Func<ISettings, NuGet.SourceRepositoryProvider> providerCreator = null)
    {
      pluginDirPath  = pluginDirPath.Collapse();
      packageDirPath = packageDirPath.Collapse();

      if (pluginDirPath.EnsureExists() == false)
        throw new ArgumentException($"Root path {pluginDirPath.FullPath} doesn't exist and couldn't be created.");

      if (packageDirPath.EnsureExists() == false)
        throw new ArgumentException($"Package path {packageDirPath.FullPath} doesn't exist and couldn't be created.");

      if (configFilePath.Directory.Exists() == false)
        throw new ArgumentException($"Config file's directory {configFilePath.Directory.FullPath} doesn't exist and couldn't be created.");

      var packageCache = await NuGetInstalledPluginRepository<TMeta>.LoadAsync(configFilePath, pluginHomeDirPath);

      return new PluginPackageManager<TMeta>(
        pluginDirPath,
        pluginHomeDirPath,
        packageDirPath,
        packageCache,
        providerCreator);
    }

    /// <summary>
    ///   Adds the specified package source. Sources added this way will be searched before any
    ///   global sources.
    /// </summary>
    /// <param name="repository">The package source to add.</param>
    public SourceRepository AddRepository(string repository) => SourceRepositories.CreateRepository(repository);

    /// <summary>
    /// Removes the specified package source.
    /// </summary>
    /// <param name="repositoryUri">The package source to remove.</param>
    /// <returns>Whether the package source was found and removed or not.</returns>
    public bool RemoveRepository(string repositoryUri)
    {
      var sr = SourceRepositories.Keys.FirstOrDefault(sr => sr.Name == repositoryUri);

      return sr != null && SourceRepositories.TryRemove(sr, out _);
    }

    /// <summary>Saves the local plugin repository state to file</summary>
    /// <returns>Success of operation</returns>
    public Task<bool> SaveConfigAsync()
    {
      return PluginRepo.SaveAsync();
    }

    /// <summary>Gets all installed versions of package <paramref name="packageId" />, if any.</summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="verify">
    ///   Whether to verify if each version, and its dependencies, are correctly
    ///   installed.
    /// </param>
    public IEnumerable<NuGetVersion> FindInstalledPluginVersions(string packageId,
                                                                 bool   verify = false)
    {
      var pkgs = PluginRepo.FindPackageById(packageId);

      if (verify)
        pkgs = pkgs.Where(p => PluginRepo.IsPluginInstalled(p.Identity, Solution));

      return pkgs.Select(p => p.Identity.Version);
    }

    public LocalPluginPackage<TMeta> FindInstalledPluginById(string packageId) => PluginRepo.FindPluginById(packageId);

    public LocalPluginPackage<TMeta> GetInstalledPlugin(PackageIdentity identity) => PluginRepo[identity];

    public IEnumerable<LocalPluginPackage<TMeta>> GetInstalledPlugins() => PluginRepo.Plugins;

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
      var project = Solution.GetPluginProject((string)packageIdentity.Id);
      var plugin  = PluginRepo[packageIdentity];

      if (project == null || plugin == null)
        throw new ArgumentException($"No such plugin {packageIdentity.Id} {packageIdentity.Version.ToNormalizedString()}");

      pluginAssemblies = plugin.GetReferencedAssembliesFilePaths(project, targetFramework ?? _currentFramework);
      dependenciesAssemblies =
        plugin.Dependencies.Values.SelectMany(i => i.GetReferencedAssembliesFilePaths(project, targetFramework ?? _currentFramework));
    }

    public Task<bool> UninstallAsync(LocalPluginPackage<TMeta> pluginPkg,
                                     bool                      removeDependencies = true,
                                     bool                      forceRemove        = false,
                                     CancellationToken         cancellationToken  = default)
    {
      try
      {
        LogTo.Trace($"Uninstall requested for plugin {pluginPkg.Id}");

        return Solution.UninstallPluginAsync(
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

    /// <summary>
    ///   Installs the latest online version of package <paramref name="onlinePackage" />.
    ///   Package's version and metadata will be derived from
    ///   <paramref name="onlinePackage.LatestOnlineVersion" /> and
    ///   <paramref name="onlinePackage.Metadata" />. Package's data, and metadata will be saved to
    ///   the package json configuration file.
    /// </summary>
    /// <param name="onlinePackage">The online package to install</param>
    /// <param name="framework">Which .NET framework should the package be compatible with</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="onlinePackage" /> is already installed</exception>
    public Task<bool> InstallAsync(
      OnlinePluginPackage<TMeta> onlinePackage,
      NuGetFramework             framework         = null,
      CancellationToken          cancellationToken = default)
    {
      return InstallAsync(
        onlinePackage,
        onlinePackage?.LatestOnlineVersion,
        framework,
        cancellationToken);
    }

    /// <summary>
    ///   Installs package <paramref name="onlinePackage" /> version
    ///   <paramref name="onlineVersion" />. Package data, and
    ///   <paramref name="onlinePackage.Metadata" /> will be saved to the package json configuration
    ///   file.
    /// </summary>
    /// <param name="onlinePackage">The online package to install</param>
    /// <param name="onlineVersion">The version of the package to install</param>
    /// <param name="framework">Which .NET framework should the package be compatible with</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="onlinePackage" /> is already installed</exception>
    public Task<bool> InstallAsync(
      OnlinePluginPackage<TMeta> onlinePackage,
      NuGetVersion               onlineVersion     = null,
      NuGetFramework             framework         = null,
      CancellationToken          cancellationToken = default)
    {
      if (onlinePackage == null)
        throw new ArgumentNullException(nameof(onlinePackage));

      return InstallAsync(
        onlinePackage.Id,
        onlinePackage.Metadata,
        new VersionRange(onlineVersion, true, onlineVersion, true),
        true,
        framework,
        cancellationToken);
    }

    /// <summary>
    ///   Installs the latest version of package <paramref name="packageId" /> that matches
    ///   <paramref name="versionRange" />. Package data, and <paramref name="metadata" /> will be
    ///   saved to the package json configuration file. If plugin already exists try to update its
    ///   version.
    /// </summary>
    /// <param name="packageId">The package name</param>
    /// <param name="metadata">Optional metadata to associate</param>
    /// <param name="versionRange">The version constraint</param>
    /// <param name="allowPrereleaseVersions">
    ///   Whether to include pre-release version in the search
    ///   results
    /// </param>
    /// <param name="framework">Which .NET framework should the package be compatible with</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="packageId" /> contains only white spaces</exception>
    public async Task<bool> InstallAsync(
      string            packageId,
      TMeta             metadata                = default,
      VersionRange      versionRange            = null,
      bool              allowPrereleaseVersions = false,
      NuGetFramework    framework               = null,
      CancellationToken cancellationToken       = default)
    {
      if (packageId == null)
        throw new ArgumentNullException(nameof(packageId));

      if (string.IsNullOrWhiteSpace(packageId))
        throw new ArgumentException($"{nameof(packageId)} contains only white spaces");

      try
      {
        var version = (await SearchMatchingVersion(packageId, versionRange, framework, cancellationToken).ConfigureAwait(false)).Max();

        if (version == null)
          return false;

        LogTo.Trace($"Install requested for plugin {packageId} {version.ToNormalizedString()}");

        // Check if this package was already installed in a previous run
        PackageIdentity packageIdentity = new PackageIdentity(packageId, version);

        // If plugin exact version already exists, abort
        if (PluginRepo.IsPluginInstalled(packageIdentity, Solution))
        {
          LogTo.Information($"Plugin {packageId} is already installed with version {version.ToNormalizedString()}");

          return true;
        }

        // If plugin already exists in a different version, try to update it
        if (PluginRepo.FindPluginById(packageId) != null)
        {
          LogTo.Information("Plugin already exist with a different version. Redirecting to UpdateAsync.");

          return await UpdateAsync();
        }

        // If plugin doesn't exist, go ahead and install it
        return await Solution.InstallPluginAsync(
          packageIdentity,
          metadata,
          allowPrereleaseVersions,
          cancellationToken);
      }
      catch (InvalidOperationException ex1) when (ex1.InnerException is OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex)
      {
        LogTo.Error(
          $"Unexpected exception while installing packages: {(ex is AggregateException aggEx ? string.Join("; ", aggEx.InnerExceptions.Select(x => x.Message)) : ex.Message)}");

        throw;
      }
    }

    /// <summary>
    ///   Attempts to update plugin <paramref name="pluginPackage" />. If no
    ///   <paramref name="version" /> is specified, the latest available version will be installed.
    /// </summary>
    /// <param name="pluginPackage">The plugin package to update</param>
    /// <param name="version">Optional version to install</param>
    /// <param name="allowPrereleaseVersions">Whether to include pre-release versions</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="pluginPackage" /> cannot be a development
    ///   plugin
    /// </exception>
    /// <exception cref="ArgumentException">Plugin <paramref name="pluginPackage" /> is not installed</exception>
    /// <exception cref="ArgumentNullException"></exception>
    public Task<bool> UpdateAsync(
      LocalPluginPackage<TMeta> pluginPackage,
      NuGetVersion              version                 = null,
      bool                      allowPrereleaseVersions = false,
      CancellationToken         cancellationToken       = default)
    {
      if (pluginPackage is LocalDevPluginPackage<TMeta>)
        throw new ArgumentException($"{nameof(pluginPackage)} cannot be a development plugin");

      return UpdateAsync(pluginPackage.Id, version, allowPrereleaseVersions, cancellationToken);
    }

    /// <summary>
    ///   Attempts to update plugin <paramref name="packageId" />. If no
    ///   <paramref name="version" /> is specified, the latest available version will be installed.
    /// </summary>
    /// <param name="packageId">The plugin package id to update</param>
    /// <param name="version">Optional version to install</param>
    /// <param name="allowPrereleaseVersions">Whether to include pre-release versions</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Success of operation</returns>
    /// <exception cref="ArgumentException"><paramref name="packageId" /> is empty</exception>
    /// <exception cref="ArgumentException">Plugin <paramref name="packageId" /> is not installed</exception>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<bool> UpdateAsync(
      string            packageId,
      NuGetVersion      version                 = null,
      bool              allowPrereleaseVersions = false,
      CancellationToken cancellationToken       = default)
    {
      if (packageId == null)
        throw new ArgumentNullException(nameof(packageId));

      if (string.IsNullOrWhiteSpace(packageId))
        throw new ArgumentException($"{nameof(packageId)} contains only white spaces");

      var pluginPackage = FindInstalledPluginById(packageId);

      if (pluginPackage == null)
        throw new ArgumentException($"Package {packageId} is not installed");

      //if (version != null && version <= pluginPackage.Identity.Version)
      //  throw new ArgumentException(
      //    $"Update version ({version.ToNormalizedString()}) needs to be higher than plugin's version ({pluginPackage.Version})");

      // If plugin exact version already exists, abort
      if (pluginPackage.Identity.Version == version)
      {
        LogTo.Information($"Plugin {packageId} is already installed with version {version.ToNormalizedString()}");

        return true;
      }

      LogTo.Trace($"Update requested for plugin {packageId} {pluginPackage.Version} -> {version.ToNormalizedString()}");

      return await Solution.UpdatePluginAsync(
        packageId,
        version,
        allowPrereleaseVersions,
        cancellationToken);
    }

    public Task<IEnumerable<PluginPackage<TMeta>>> Search(
      string                             searchTerm,
      bool                               enablePreRelease           = false,
      Func<string, TMeta>                getMetaFromPackageNameFunc = null,
      Func<IPackageSearchMetadata, bool> filterSearchResultFunc     = null,
      CancellationToken                  cancellationToken          = default)
    {
      if (searchTerm == null)
        throw new ArgumentNullException(nameof(searchTerm));

      return Search(searchTerm,
                    SourceRepositories,
                    enablePreRelease,
                    getMetaFromPackageNameFunc,
                    filterSearchResultFunc,
                    cancellationToken);
    }

    public Task<IEnumerable<PluginPackage<TMeta>>> Search(
      string                             searchTerm,
      ISourceRepositoryProvider          provider,
      bool                               enablePreRelease           = false,
      Func<string, TMeta>                getMetaFromPackageNameFunc = null,
      Func<IPackageSearchMetadata, bool> filterSearchResultFunc     = null,
      CancellationToken                  cancellationToken          = default)
    {
      if (searchTerm == null)
        throw new ArgumentNullException(nameof(searchTerm));

      if (provider == null)
        throw new ArgumentNullException(nameof(provider));

      return Search(searchTerm,
                    provider.GetRepositories(),
                    enablePreRelease,
                    getMetaFromPackageNameFunc,
                    filterSearchResultFunc,
                    cancellationToken);
    }

    public async Task<IEnumerable<PluginPackage<TMeta>>> Search(
      string                             searchTerm,
      IEnumerable<SourceRepository>      repositories,
      bool                               enablePreRelease           = false,
      Func<string, TMeta>                getMetaFromPackageNameFunc = null,
      Func<IPackageSearchMetadata, bool> filterSearchResultFunc     = null,
      CancellationToken                  cancellationToken          = default)
    {
      if (searchTerm == null)
        throw new ArgumentNullException(nameof(searchTerm));

      if (repositories == null)
        throw new ArgumentNullException(nameof(repositories));

      var tasks = repositories.Select(
        r => SearchInternal(searchTerm,
                            r,
                            enablePreRelease,
                            filterSearchResultFunc,
                            cancellationToken)
      );

      var onlinePkgs = (await Task.WhenAll(tasks)).SelectMany(a => a);

      return await SearchInternalCreatePackages(onlinePkgs, getMetaFromPackageNameFunc);
    }

    public async Task<List<PluginPackage<TMeta>>> Search(
      string                             searchTerm,
      SourceRepository                   sourceRepository,
      bool                               enablePreRelease           = false,
      Func<string, TMeta>                getMetaFromPackageNameFunc = null,
      Func<IPackageSearchMetadata, bool> filterSearchResultFunc     = null,
      CancellationToken                  cancellationToken          = default)
    {
      var onlinePkgs = await SearchInternal(
        searchTerm,
        sourceRepository,
        enablePreRelease,
        filterSearchResultFunc,
        cancellationToken);

      return await SearchInternalCreatePackages(onlinePkgs, getMetaFromPackageNameFunc);
    }

    private async Task<List<IPackageSearchMetadata>> SearchInternal(
      string                             searchTerm,
      SourceRepository                   sourceRepository,
      bool                               enablePreRelease       = false,
      Func<IPackageSearchMetadata, bool> filterSearchResultFunc = null,
      CancellationToken                  cancellationToken      = default)
    {
      if (searchTerm == null)
        throw new ArgumentNullException(nameof(searchTerm));

      if (sourceRepository == null)
        throw new ArgumentNullException(nameof(sourceRepository));

      var pkgSearchRes = await sourceRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);

      // Match all online plugins with those listed in pluginMetadatas
      var onlinePkgs = (await pkgSearchRes.SearchAsync(
        searchTerm,
        new SearchFilter(enablePreRelease),
        0, 200,
        _logger,
        cancellationToken)).ToList();

      if (filterSearchResultFunc != null)
        onlinePkgs = onlinePkgs.Where(filterSearchResultFunc)
                               .ToList();

      return onlinePkgs;
    }

    private async Task<List<PluginPackage<TMeta>>> SearchInternalCreatePackages(
      IEnumerable<IPackageSearchMetadata> onlinePkgs,
      Func<string, TMeta>                 getMetaFromPackageNameFunc)
    {
      TMeta GetDefaultMeta(string _)
      {
        return default;
      }

      getMetaFromPackageNameFunc ??= GetDefaultMeta;

      // Fetch all available versions
      var pkgVersionDownloadTasks = onlinePkgs.ToDictionary(pkg => pkg, pkg => pkg.GetVersionsAsync());

      await Task.WhenAll(pkgVersionDownloadTasks.Values);

      var onlinePkgVersions = pkgVersionDownloadTasks
                              .GroupBy(kvp => kvp.Key.Identity.Id)
                              .ToDictionary(g => g.First().Key,
                                            g => g.Aggregate(
                                              new List<VersionInfo>(),
                                              (vList, kvp) =>
                                              {
                                                vList.AddRange(kvp.Value.Result);
                                                return vList;
                                              }
                                            ));

      // Match local packages with online ones
      var localPkgs = GetInstalledPlugins().ToDictionary(k => k.Identity.Id);

      // TODO: Match Package version with Interop version (see PackageDependency)

      PluginPackage<TMeta> CreatePackage(KeyValuePair<IPackageSearchMetadata, List<VersionInfo>> onlinePackage)
      {
        var packageId = onlinePackage.Key.Identity.Id;
        var packageVersions = onlinePackage.Value?.Any() ?? false
          ? onlinePackage.Value
          : new List<VersionInfo> { new VersionInfo(onlinePackage.Key.Identity.Version) };

        if (localPkgs.ContainsKey(packageId) == false)
          return new OnlinePluginPackage<TMeta>(packageId, getMetaFromPackageNameFunc(packageId), packageVersions);

        var localPkg = localPkgs[packageId];
        localPkg.SetOnlineVersions(packageVersions);

        return localPkg;
      }

      return onlinePkgVersions.Select(CreatePackage)
                              .ToList();
    }

    public Task<IEnumerable<NuGetVersion>> SearchMatchingVersion(
      string            packageId,
      VersionRange      versionRange      = null,
      NuGetFramework    framework         = null,
      CancellationToken cancellationToken = default)
    {
      return SearchMatchingVersion(SourceRepositories, packageId, versionRange, framework, cancellationToken);
    }

    public Task<IEnumerable<NuGetVersion>> SearchMatchingVersion(
      ISourceRepositoryProvider provider,
      string                    packageId,
      VersionRange              versionRange      = null,
      NuGetFramework            framework         = null,
      CancellationToken         cancellationToken = default)
    {
      return SearchMatchingVersion(provider.GetRepositories(), packageId, versionRange, framework,
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
