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
// Created On:   2021/04/04 17:05
// Modified On:  2021/04/15 22:35
// Modified By:  Alexis

#endregion




namespace PluginManager.PackageManager.NuGet
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Linq;
  using global::NuGet.Configuration;
  using global::NuGet.Protocol;
  using global::NuGet.Protocol.Core.Types;

  public class NuGetSourceRepositoryProvider : SourceRepositoryProvider
  {
    #region Constants & Statics

    private static readonly IEnumerable<string> _defaultSources = new List<string>
    {
      "https://api.nuget.org/v3/index.json"
    };

    #endregion




    #region Constructors

    /// <inheritdoc />
    public NuGetSourceRepositoryProvider(ISettings settings) : base(settings, _defaultSources) { }

    #endregion
  }

  /// <summary>
  ///   Creates and caches SourceRepository objects, which are the combination of
  ///   PackageSource instances with a set of supported resource providers. It also manages the set
  ///   of default source repositories. Original from: https://github.com/Wyamio/Wyam/ Copyright (c)
  ///   2014 Dave Glick
  /// </summary>
  public class SourceRepositoryProvider : ConcurrentDictionary<PackageSource, SourceRepository>, ISourceRepositoryProvider
  {
    #region Properties & Fields - Non-Public

    private readonly List<Lazy<INuGetResourceProvider>> _resourceProviders;

    #endregion




    #region Constructors

    /// <summary>New instance</summary>
    /// <param name="settings"></param>
    /// <param name="defaultSources"></param>
    public SourceRepositoryProvider(ISettings           settings,
                                    IEnumerable<string> defaultSources = null)
    {
      // Create the package source provider (needed primarily to get default sources)
      PackageSourceProvider = new PackageSourceProvider(settings);

      // Add the v3 provider as default
      _resourceProviders = new List<Lazy<INuGetResourceProvider>>();
      _resourceProviders.AddRange(Repository.Provider.GetCoreV3());

      if (defaultSources == null)
        return;

      foreach (var src in defaultSources.Distinct())
        CreateRepository(src);
    }

    #endregion




    #region Properties Impl - Public

    /// <inheritdoc />
    public IPackageSourceProvider PackageSourceProvider { get; }

    #endregion




    #region Methods Impl

    /// <summary>Creates or gets a non-default source repository by PackageSource.</summary>
    public SourceRepository CreateRepository(PackageSource packageSource) => CreateRepository(packageSource, FeedType.Undefined);

    /// <summary>Creates or gets a non-default source repository by PackageSource.</summary>
    public SourceRepository CreateRepository(PackageSource packageSource,
                                             FeedType      feedType) =>
      GetOrAdd(packageSource, x => new SourceRepository(x, _resourceProviders)); // TODO: Act based on feedType ?

    /// <summary>Gets all cached repositories.</summary>
    public IEnumerable<SourceRepository> GetRepositories() => Values;

    #endregion




    #region Methods

    /// <summary>
    ///   Gets all cached repositories which are enabled, see
    ///   <see cref="SourceRepository.PackageSource" /> and <see cref="PackageSource.IsEnabled" />.
    /// </summary>
    public IEnumerable<SourceRepository> GetEnabledRepositories() => GetRepositories().Where(sr => sr.PackageSource.IsEnabled);

    /// <summary>Creates or gets a non-default source repository.</summary>
    public SourceRepository CreateRepository(string packageSource) =>
      CreateRepository(new PackageSource(packageSource));

    #endregion
  }
}
