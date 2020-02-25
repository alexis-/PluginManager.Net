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
// Modified On:  2020/02/25 11:59
// Modified By:  Alexis

#endregion




using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NuGet.Common;

namespace PluginManager.Logger
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  public class PluginManagerLogger : ILogger, ILogAdapter
  {
    #region Constants & Statics

    public static ILogAdapter UserLogger { get; set; }

    #endregion




    #region Nuget.Common.ILogger

    public void LogDebug(string data) => Debug(data);

    public void LogVerbose(string data) => Trace(data);

    public void LogInformation(string data) => Information(data);

    public void LogInformationSummary(string data) => Information(data);

    public void LogMinimal(string data) => Trace(data);

    public void LogWarning(string data) => Warning(data);

    public void LogError(string data) => Error(data);

    public void Log(ILogMessage message) => Log(message.Level, message.Message);

    public void Log(LogLevel level,
                    string   data)
    {
      switch (level)
      {
        case LogLevel.Error:
          Error(data);
          break;

        case LogLevel.Warning:
          Warning(data);
          break;

        case LogLevel.Information:
          Information(data);
          break;

        case LogLevel.Debug:
          Debug(data);
          break;

        case LogLevel.Verbose:
          Trace(data);
          break;

        case LogLevel.Minimal:
          Trace(data);
          break;
      }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task LogAsync(LogLevel level, string data) => Log(level, data);

    public async Task LogAsync(ILogMessage message) => Log(message.Level, message.Message);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    #endregion




    #region IAnotarLogger impl

    public bool IsTraceEnabled       { get; set; }
    public bool IsDebugEnabled       { get; set; }
    public bool IsInformationEnabled { get; set; }
    public bool IsWarningEnabled     { get; set; }
    public bool IsErrorEnabled       { get; set; }
    public bool IsFatalEnabled       { get; set; }

    public void Trace(string message)
    {
      UserLogger?.Trace(message);
    }

    public void Trace(string format, params object[] args)
    {
      UserLogger?.Trace(format, args);
    }

    public void Trace(Exception exception, string format, params object[] args)
    {
      UserLogger?.Trace(exception, format, args);
    }

    public void Debug(string message)
    {
      UserLogger?.Debug(message);
    }

    public void Debug(string format, params object[] args)
    {
      UserLogger?.Debug(format, args);
    }

    public void Debug(Exception exception, string format, params object[] args)
    {
      UserLogger?.Debug(exception, format, args);
    }

    public void Information(string message)
    {
      UserLogger?.Information(message);
    }

    public void Information(string format, params object[] args)
    {
      UserLogger?.Information(format, args);
    }

    public void Information(Exception exception, string format, params object[] args)
    {
      UserLogger?.Information(exception, format, args);
    }

    public void Warning(string message)
    {
      UserLogger?.Warning(message);
    }

    public void Warning(string format, params object[] args)
    {
      UserLogger?.Warning(format, args);
    }

    public void Warning(Exception exception, string format, params object[] args)
    {
      UserLogger?.Warning(exception, format, args);
    }

    public void Error(string message)
    {
      UserLogger?.Error(message);
    }

    public void Error(string format, params object[] args)
    {
      UserLogger?.Error(format, args);
    }

    public void Error(Exception exception, string format, params object[] args)
    {
      UserLogger?.Error(exception, format, args);
    }

    public void Fatal(string message)
    {
      UserLogger?.Fatal(message);
    }

    public void Fatal(string format, params object[] args)
    {
      UserLogger?.Fatal(format, args);
    }

    public void Fatal(Exception exception, string format, params object[] args)
    {
      UserLogger?.Fatal(exception, format, args);
    }

    #endregion
  }
}
