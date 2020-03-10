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
// Modified On:  2020/02/27 15:49
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Anotar.Custom;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using PluginManager.Contracts;
using PluginManager.Extensions;
using PluginManager.PackageManager;
using PluginManager.PackageManager.Models;

namespace PluginManager.Services
{
  /// <inheritdoc/>
  public abstract class DefaultPluginRepositoryService<TMeta>
    : IPluginRepositoryService<TMeta>
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    protected DefaultPluginRepositoryService() { }

    #endregion




    #region Methods Impl

    /// <summary>
    ///   Fetch the metadata of all plugin indexed by the API pointed to by
    ///   <see cref="UpdateUrl" />
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Plugin metadatas or <see langword="null" /></returns>
    public virtual async Task<List<TMeta>> FetchPluginMetadataList(CancellationToken cancellationToken = default)
    {
      try
      {
        using (var client = CreateHttpClient())
        {
          SetHttpClientHeaders(client);

          var resp = await client.GetAsync(UpdateUrl, cancellationToken);

          if (resp == null || resp.StatusCode != System.Net.HttpStatusCode.OK)
          {
            LogTo.Warning($"Failed to download plugin list from plugin repository at {UpdateUrl}");
            return null;
          }

          var respStringContent = await resp.Content.ReadAsStringAsync();

          try
          {
            var pluginMetadatas = DeserializePluginMetadataList(respStringContent);

            LogTo.Debug($"Fetched {pluginMetadatas.Count} plugins");
            LogTo.Trace($"Fetched plugins:\n{pluginMetadatas.Serialize(Formatting.Indented)}");

            return pluginMetadatas;
          }
          catch (JsonException ex)
          {
            LogTo.Warning(ex, $"Failed to deserialize plugin repository list response");
            return null;
          }
        }
      }
      catch (HttpRequestException ex)
      {
        LogTo.Warning(ex, $"Failed to fetch plugin from plugin repository at {UpdateUrl}");
      }
      catch (Exception ex)
      {
        LogTo.Error(ex, $"An exception was thrown while fetching plugin from plugin repository at {UpdateUrl}");
      }

      return null;
    }

    /// <summary>
    ///   Search available NuGet repositories for all packages matching
    ///   <paramref name="searchTerm" /> and <paramref name="enablePrerelease" />. Only NuGet packages
    ///   that are also indexed by the API pointed to by <see cref="UpdateUrl" /> will be included.
    /// </summary>
    /// <param name="searchTerm">Part or totality of the package name to look for</param>
    /// <param name="enablePrerelease">Whether to include packages that are marked as pre-release</param>
    /// <param name="pm">The package manager</param>
    /// <param name="cancellationToken"></param>
    /// <returns>All available packages or <see langword="null" /></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public virtual async Task<IEnumerable<PluginPackage<TMeta>>> SearchPlugins(
      string                      searchTerm,
      bool                        enablePrerelease,
      PluginPackageManager<TMeta> pm,
      CancellationToken           cancellationToken = default)
    {
      if (pm == null)
      {
        LogTo.Error("pm is NULL");
        throw new ArgumentNullException(nameof(pm));
      }

      var allowedPluginMetadatas = await FetchPluginMetadataList(cancellationToken);

      if (allowedPluginMetadatas == null)
      {
        LogTo.Warning($"Fetching plugin metadatas from repository failed while searching plugins with term '{searchTerm}'. Aborting");
        return null;
      }

      if (allowedPluginMetadatas.Any() == false)
        return new List<PluginPackage<TMeta>>();

      var allowedPluginMetadataMap = allowedPluginMetadatas.ToDictionary(GetPackageIdFromMetadata);

      var plugins = (await pm.Search(
          searchTerm,
          enablePrerelease,
          pn => GetMetaFromPackageName(pn, allowedPluginMetadataMap),
          psm => FilterSearchResult(psm, allowedPluginMetadataMap),
          cancellationToken))
        ?.ToList();

      if (plugins == null)
      {
        LogTo.Warning($"Failed to fetch plugins from the NuGet repositories while searching for term '{searchTerm}'");
        return null;
      }

      LogTo.Trace($"Found {plugins.Count} plugins:\n{plugins.Serialize(Formatting.Indented)}");

      return plugins;
    }

    #endregion




    #region Methods

    /// <summary>Override to create a custom HttpClient instance</summary>
    /// <returns><see cref="HttpClient" /> instance</returns>
    protected virtual HttpClient CreateHttpClient()
    {
      return new HttpClient();
    }

    /// <summary>Override to set custom http client headers</summary>
    /// <param name="client">The <see cref="HttpClient" /> instance</param>
    protected virtual void SetHttpClientHeaders(HttpClient client)
    {
      client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
      client.DefaultRequestHeaders.Add("AcceptLanguage", "en-GB,*");
      client.DefaultRequestHeaders.Add("AcceptEncoding", "gzip, deflate");
    }

    /// <summary>
    ///   Deserialize the API response's content from
    ///   <see cref="FetchPluginMetadataList(CancellationToken)" />
    /// </summary>
    /// <param name="response">The response content</param>
    /// <returns>Deserialized list of metadata</returns>
    protected virtual List<TMeta> DeserializePluginMetadataList(string response)
    {
      return response.Deserialize<List<TMeta>>();
    }

    /// <summary>
    ///   Associates a <typeparamref name="TMeta" /> to <paramref name="packageName" />. Override to
    ///   change default behaviour (simple dictionary lookup).
    /// </summary>
    /// <param name="packageName">Name of a package available on the NuGet repository</param>
    /// <param name="allowedPluginMetadataMap">
    ///   A map of package name and plugin metadata returned from
    ///   the API
    /// </param>
    /// <returns>The metadata to associate with package <paramref name="packageName" /></returns>
    protected virtual TMeta GetMetaFromPackageName(string packageName, Dictionary<string, TMeta> allowedPluginMetadataMap)
    {
      return allowedPluginMetadataMap.SafeGet(packageName);
    }

    /// <summary>
    ///   Filters which packages will be returned from
    ///   <see cref="SearchPlugins(string, bool, PluginPackageManager{TMeta}, CancellationToken)" />.
    ///   Override to change default behaviour (simple dictionary lookup).
    /// </summary>
    /// <param name="searchResult">Data about a package available on the NuGet repository</param>
    /// <param name="allowedPluginMetadataMap">
    ///   A map of package name and plugin metadata returned from
    ///   the API
    /// </param>
    /// <returns></returns>
    protected virtual bool FilterSearchResult(IPackageSearchMetadata searchResult, Dictionary<string, TMeta> allowedPluginMetadataMap)
    {
      return allowedPluginMetadataMap.ContainsKey(searchResult.Identity.Id);
    }

    #endregion




    #region Methods Abs

    /// <summary>
    ///   Predicate which returns the package name for the given <paramref name="metadata" />.
    ///   This method is required by
    ///   <see cref="SearchPlugins(string, bool, PluginPackageManager{TMeta}, CancellationToken)" />
    /// </summary>
    /// <param name="metadata">The metadata instance</param>
    /// <returns>The name of the package which <typeparamref name="TMeta" /> belongs to</returns>
    protected abstract string GetPackageIdFromMetadata(TMeta metadata);

    /// <summary>API GET endpoint</summary>
    public abstract string UpdateUrl { get; }

    /// <inheritdoc />
    public abstract bool UpdateEnabled { get; }

    #endregion
  }
}
