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
// Modified On:  2020/02/25 00:55
// Modified By:  Alexis

#endregion




using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommandLine;
using PluginManager.PluginHost;

// ReSharper disable HeuristicUnreachableCode

namespace PluginHost
{
  /// <summary>Interaction logic for App.xaml</summary>
  public partial class App : Application
  {
    #region Properties & Fields - Non-Public

    private IDisposable _pluginHost;

    #endregion




    #region Methods Impl

    protected override void OnExit(ExitEventArgs e)
    {
      _pluginHost?.Dispose();

      base.OnExit(e);
    }

    #endregion




    #region Methods

    private void Application_Startup(object           sender,
                                     StartupEventArgs e)
    {
      RedirectExceptionsToErrorOutput();

      try
      {
        Parser.Default.ParseArguments<PluginHostParameters>(e.Args)
              .WithParsed(LoadPlugin)
              .WithNotParsed(_ =>
              {
                if (Environment.GetCommandLineArgs().Length > 1)
                  Console.Error.WriteLine($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(a => $"\"{a}\""))}");

                Shutdown(PluginHostConst.ExitParameters);
              });
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"PluginHost crashed with exception: {ex}");
        Shutdown(PluginHostConst.ExitUnknownError);
      }
    }

    private void LoadPlugin(PluginHostParameters args)
    {
      Process pluginMgrProcess;

      if (File.Exists(Path.Combine(args.HomePath, "debugger")))
        Debugger.Launch();

      try
      {
        pluginMgrProcess = Process.GetProcessById(args.ManagerProcessId);
      }
      catch (Exception)
      {
        Shutdown(PluginHostConst.ExitParentExited);
        return;
      }

      _pluginHost = PluginLoader.Create(
        args.PackageRootFolder,
        args.PluginAndDependenciesAssembliesPath,
        args.PluginHostTypeAssemblyName,
        args.PluginHostTypeQualifiedName,
        args.PackageName,
        args.HomePath,
        args.SessionGuid,
        args.ChannelName,
        pluginMgrProcess,
        args.IsDevelopment);
    }

    private void RedirectExceptionsToErrorOutput()
    {
      AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        Console.Error.WriteLine(
          @$"Unhandled exception. Is terminating ? {ev.IsTerminating}
----------------------------------------
{ev.ExceptionObject}
----------------------------------------");

      DispatcherUnhandledException += (o, ev) => Console.Error.WriteLine(
        @$"Unhandled exception.
----------------------------------------
{ev.Exception}
----------------------------------------");
    }

    #endregion
  }
}
