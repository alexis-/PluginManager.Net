// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="">
//   
// </copyright>
// <summary>
//   The program.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

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
// Created On:   2021/03/25 09:15
// Modified On:  2021/03/25 14:42
// Modified By:  Alexis

#endregion




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
// Created On:   2021/03/25 09:15
// Modified On:  2021/03/25 13:34
// Modified By:  Alexis

#endregion




namespace PluginHost
{
  using System;
  using System.Linq;
  using System.Threading;
  using CommandLine;
  using Exceptions;
  using Plugin;
  using PluginManager.PluginHost;

  /// <summary>The program.</summary>
  public class Program
  {
    #region Methods

    /// <summary>
    /// PluginHost entry point
    /// </summary>
    /// <param name="args">
    /// Start parameters
    /// </param>
    private static int Main(string[] args)
    {
      try
      {
        RedirectExceptionsToErrorOutput();

        CancellationTokenSource cts = new CancellationTokenSource();

        Parser.Default.ParseArguments<PluginHostParameters>(args)
              .WithParsed(php => PluginLoader.Create(php, cts))
              .WithNotParsed(errors =>
              {
                foreach (var error in errors)
                  Console.Error.WriteLine(error.ToString());

                if (args.Length > 1)
                  Console.Error.WriteLine(
                    $"\nReceived arguments: {string.Join(" ", args.Select(a => $"\"{a}\""))}");

                throw new PluginHostException(PluginHostConst.ExitParameters);
              });

        WaitHandle.WaitAny(new[] { cts.Token.WaitHandle });
      }
      catch (PluginHostException ex)
      {
        return ex.ErrorCode;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"PluginHost crashed with exception: {ex}");
        return PluginHostConst.ExitUnknownError;
      }

      return 0;
    }

    /// <summary>The redirect exceptions to error output.</summary>
    private static void RedirectExceptionsToErrorOutput()
    {
      AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        Console.Error.WriteLine(
          $@"Unhandled exception. Is terminating ? {ev.IsTerminating}
----------------------------------------
{ev.ExceptionObject}
----------------------------------------");
    }

    #endregion
  }
}
