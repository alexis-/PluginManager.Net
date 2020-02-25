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
// Modified On:  2020/02/24 15:51
// Modified By:  Alexis

#endregion




using Extensions.System.IO;

namespace PluginManager.Contracts
{
  /// <summary>
  ///   A contract interface which returns the paths to various key folders and files used by
  ///   the PluginManager. This should be implemented by project users.
  /// </summary>
  public interface IPluginLocations
  {
    /// <summary>
    ///   The root folder of all Plugin files. Other properties in this interface should be
    ///   children of this directory
    /// </summary>
    DirectoryPath PluginDir { get; }

    /// <summary>
    ///   The home directory of each plugin, similar to the ~ for users. This is the personal
    ///   filespace of plugins
    /// </summary>
    DirectoryPath PluginHomeDir { get; }

    /// <summary>The directory which holds all the NuGet packages</summary>
    DirectoryPath PluginPackageDir { get; }

    /// <summary>The directory which contains development plugins</summary>
    DirectoryPath PluginDevelopmentDir { get; }

    /// <summary>Path to the PluginHost.exe file</summary>
    FilePath PluginHostExeFile { get; }

    /// <summary>Path to the plugin .json configuration file</summary>
    FilePath PluginConfigFile { get; }
  }
}
