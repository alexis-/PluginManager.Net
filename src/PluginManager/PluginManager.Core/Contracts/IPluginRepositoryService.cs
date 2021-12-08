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
// Modified On:  2020/02/26 20:45
// Modified By:  Alexis

#endregion




using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PluginManager.PackageManager;
using PluginManager.PackageManager.Models;

namespace PluginManager.Contracts
{
  /// <summary>
  /// Contract interface used by the Plugin Manager to fetch available plugin from your API endpoint.
  /// </summary>
  /// <typeparam name="TMeta">The Metadata type which define data associated with each plugin</typeparam>
  public interface IPluginRepositoryService<TMeta>
  {
    /// <summary>Whether plugin updates are enabled</summary>
    bool UpdateEnabled { get; }

    /// <summary>Fetch the metadata of all plugin indexed by the API</summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Plugin metadatas or <see langword="null" /></returns>
    Task<List<TMeta>> FetchPluginMetadataList(CancellationToken cancellationToken = default);

    /// <summary>
    ///   Search available NuGet repositories for all packages matching
    ///   <paramref name="searchTerm" /> and <paramref name="enablePreRelease" />. Only NuGet packages
    ///   that are also indexed by the API should be included.
    /// </summary>
    /// <param name="searchTerm">Part or totality of the package name to look for</param>
    /// <param name="enablePreRelease">Whether to include packages that are marked as pre-release</param>
    /// <param name="pm">The package manager</param>
    /// <param name="cancellationToken"></param>
    /// <returns>All available packages or <see langword="null" /></returns>
    Task<IEnumerable<PluginPackage<TMeta>>> SearchPlugins(
      string                      searchTerm,
      bool                        enablePreRelease,
      PluginPackageManager<TMeta> pm,
      CancellationToken           cancellationToken);
  }
}
