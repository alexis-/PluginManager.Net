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
// Modified On:  2020/02/24 17:29
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Anotar.Custom;
using Newtonsoft.Json;
using PluginManager.Contracts;
using PluginManager.Extensions;

namespace PluginManager.Services
{
  public abstract class DefaultPluginRepositoryService<TMeta>
    : IPluginRepositoryService<TMeta>
  {
    #region Constructors

    protected DefaultPluginRepositoryService() { }

    #endregion




    #region Methods Impl

    public async Task<Dictionary<string, TMeta>> ListPlugins()
    {
      try
      {
        using (HttpClient client = CreateHttpClient())
        {
          SetHttpClientHeaders(client);

          var resp = await client.GetAsync(UpdateUrl);

          if (resp == null || resp.StatusCode != System.Net.HttpStatusCode.OK)
          {
            LogTo.Warning($"Failed to download plugin list from plugin repository at {UpdateUrl}");
            return null;
          }

          var respStringContent = await resp.Content.ReadAsStringAsync();

          try
          {
            var metadatas = respStringContent.Deserialize<List<TMeta>>();

            return ToIdMetadataDictionary(metadatas);
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

    #endregion




    public abstract Dictionary<string, TMeta> ToIdMetadataDictionary(List<TMeta> metadatas);


    #region Methods

    public virtual HttpClient CreateHttpClient()
    {
      return new HttpClient();
    }

    public virtual void SetHttpClientHeaders(HttpClient client)
    {
      client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
      client.DefaultRequestHeaders.Add("AcceptLanguage", "en-GB,*");
      client.DefaultRequestHeaders.Add("AcceptEncoding", "gzip, deflate");
    }

    #endregion




    #region Methods Abs

    public abstract string UpdateUrl     { get; }
    public abstract bool   UpdateEnabled { get; }

    #endregion
  }
}
