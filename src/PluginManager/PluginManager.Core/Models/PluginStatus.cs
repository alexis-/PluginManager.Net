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
// Modified On:  2020/03/04 14:36
// Modified By:  Alexis

#endregion




using System;

namespace PluginManager.Models
{
  /// <summary>The current status of the plugin process</summary>
  [Serializable]
  public enum PluginStatus
  {
    /// <summary>
    ///   Set immediately after checking a plugin passes the pre-requisites to be started, and
    ///   before its process is created.
    /// </summary>
    Starting,

    /// <summary>
    ///   Set after a plugin process has been started, and connection has been made with the
    ///   Plugin Manager instance.
    /// </summary>
    Connected,

    /// <summary>
    ///   Set immediately after checking a plugin passes the pre-requisites to be stopped, and
    ///   before sending it the stop signal.
    /// </summary>
    Stopping,

    /// <summary>Set when a plugin wasn't started, or after a plugin has been stopped or killed.</summary>
    Stopped,
  }
}
