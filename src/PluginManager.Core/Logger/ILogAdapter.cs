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
// Modified On:  2020/02/23 02:56
// Modified By:  Alexis

#endregion




using System;

namespace PluginManager.Logger
{
  public interface ILogAdapter
  {
    bool IsTraceEnabled       { get; }
    bool IsDebugEnabled       { get; }
    bool IsInformationEnabled { get; }
    bool IsWarningEnabled     { get; }
    bool IsErrorEnabled       { get; }
    bool IsFatalEnabled       { get; }

    void Trace(string    message);
    void Trace(string    format,    params object[] args);
    void Trace(Exception exception, string          format, params object[] args);


    void Debug(string    message);
    void Debug(string    format,    params object[] args);
    void Debug(Exception exception, string          format, params object[] args);

    void Information(string    message);
    void Information(string    format,    params object[] args);
    void Information(Exception exception, string          format, params object[] args);


    void Warning(string    message);
    void Warning(string    format,    params object[] args);
    void Warning(Exception exception, string          format, params object[] args);

    void Error(string    message);
    void Error(string    format,    params object[] args);
    void Error(Exception exception, string          format, params object[] args);

    void Fatal(string    message);
    void Fatal(string    format,    params object[] args);
    void Fatal(Exception exception, string          format, params object[] args);
  }
}
