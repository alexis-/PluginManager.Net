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
// Modified On:  2020/02/25 02:07
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Anotar.Custom;
using CommandLine;
using Extensions.System.IO;
using PluginManager.Extensions;
using PluginManager.Models;
using PluginManager.PluginHost;
using PluginManager.Sys.Threading;

namespace PluginManager
{
  public abstract partial class PluginManagerBase<TParent, TPluginInstance, TMeta, ICustomPluginManager, ICore, IPlugin>
  {
    #region Constants & Statics

    public virtual int PluginStopTimeout { get; } = 3000;

#if DEBUG
    public virtual int PluginConnectTimeout { get; } = 300000;
#else
    public virtual int PluginConnectTimeout { get; } = 10000;
#endif

    #endregion




    #region Methods

    /// <summary>
    /// Start plugin <paramref name="pluginInstance"/>
    /// </summary>
    /// <param name="pluginInstance">The plugin to start</param>
    /// <returns>Success of operation</returns>
    public async Task<bool> StartPlugin(TPluginInstance pluginInstance)
    {
      var pluginPackage = pluginInstance.Package;
      var packageName   = pluginPackage.Id;

      try
      {
        // Make sure the plugin is stopped and can be started
        using (await pluginInstance.Lock.LockAsync())
        {
          if (pluginInstance.Status != PluginStatus.Stopped)
            return true;

          if (CanPluginStartOrPause(pluginInstance) == false)
            throw new InvalidOperationException("A plugin with the same Package name is already running");

          OnPluginStarting(pluginInstance);
        }
        
        // Determine the plugin and its dependencies assemblies' information. This includes the assembly which contains the PluginHost type
        string packageRootFolder = Locations.PluginPackageDir.FullPathWin;
        List<string> pluginAndDependenciesAssembliesPath = new List<string>();
        string pluginHostTypeAssemblyName = GetPluginHostTypeAssemblyName(pluginInstance);
        
        if (pluginInstance.IsDevelopment == false)
        {
          using (await PMLock.ReaderLockAsync())
          {
            List<FilePath> pluginAndDependenciesPackageFilePaths = new List<FilePath>();
            var pluginPkg = PackageManager.FindInstalledPluginById(packageName);

            if (pluginPkg == null)
              throw new InvalidOperationException($"Cannot find requested plugin package {packageName}");

            PackageManager.GetInstalledPluginAssembliesFilePath(
              pluginPkg.Identity,
              out var tmpPluginAssemblies,
              out var tmpDependenciesAssemblies);

            pluginAndDependenciesPackageFilePaths.AddRange(tmpPluginAssemblies);
            pluginAndDependenciesPackageFilePaths.AddRange(tmpDependenciesAssemblies);

            if (pluginAndDependenciesPackageFilePaths.Any(a => a.FileNameWithoutExtension == pluginHostTypeAssemblyName) == false)
            {
              LogTo.Warning($"Unable to find the PluginHost type's \"{pluginHostTypeAssemblyName}\" dependency assembly's package");
              return false;
            }

            foreach (var pkgFilePath in pluginAndDependenciesPackageFilePaths)
            {
              string pkgRelativeFilePath = pkgFilePath.FullPathWin.After(packageRootFolder);

              if (string.IsNullOrWhiteSpace(pkgRelativeFilePath))
              {
                LogTo.Warning(
                  $"Package {pkgFilePath} isn't located underneath the package folder {packageRootFolder}. Skipping, this might cause issues with the plugin");
                continue;
              }

              pluginAndDependenciesAssembliesPath.Add(pkgRelativeFilePath);
            }
          }
        }

        // Build command line
        var cmdLineParams = new PluginHostParameters
        {
          PackageRootFolder             = packageRootFolder,
          PluginAndDependenciesAssembliesPath = string.Join(";", pluginAndDependenciesAssembliesPath),
          PluginHostTypeAssemblyName    = pluginHostTypeAssemblyName,
          PluginHostTypeQualifiedName   = GetPluginHostTypeQualifiedName(pluginInstance),
          PackageName                   = packageName,
          HomePath                      = pluginPackage.HomeDir.FullPath,
          SessionString                 = pluginInstance.Guid.ToString(),
          ChannelName                   = IpcServerChannelName,
          ManagerProcessId              = Process.GetCurrentProcess().Id,
          IsDevelopment                 = pluginInstance.IsDevelopment,
        };

        // Build process parameters
        var processArgs = Parser.Default.FormatCommandLine(cmdLineParams);

        pluginInstance.Process = new Process
        {
          StartInfo = new ProcessStartInfo(
            Locations.PluginHostExeFile.FullPath,
            processArgs)
          {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
          },
          EnableRaisingEvents = true,
        };

        // Setup error output logging
        {
          var pluginStr = pluginInstance.ToString(); // Avoids keeping a pointer to PluginInstance around
          StringBuilder pluginErrBuilder = new StringBuilder();

          void LogPluginErrorOutput()
          {
            if (string.IsNullOrWhiteSpace(pluginErrBuilder.ToString()))
              return;

            lock (pluginErrBuilder)
            {
              LogTo.Warning($"{pluginStr} standard error output:\n--------------------------------------------\n{pluginErrBuilder.ToString().Trim()}\n--------------------------------------------");
              pluginErrBuilder.Clear();
            }
          }
          
          DelayedTask logTask = new DelayedTask(LogPluginErrorOutput, 200);

          void AggregatePluginErrorOutput(object _, DataReceivedEventArgs e)
          {
            lock (pluginErrBuilder)
            {
              pluginErrBuilder.AppendLine(e.Data);
              logTask.Trigger(750);
            }
          }

          pluginInstance.Process.ErrorDataReceived += AggregatePluginErrorOutput;
        }

        // Start plugin
        if (pluginInstance.Process.Start() == false)
        {
          LogTo.Warning($"Failed to start process for {pluginInstance}");
          return false;
        }

        OnPluginStarted(pluginInstance);

        pluginInstance.Process.EnableRaisingEvents = true;
        pluginInstance.Process.BeginErrorReadLine();
        pluginInstance.Process.Exited += (o, e) =>
        {
          UISynchronizationContext.Post(_ =>
          {
            using (pluginInstance.Lock.Lock())
              OnPluginStopped(pluginInstance);
          }, null);
        };

        if (await pluginInstance.ConnectedEvent.WaitAsync(PluginConnectTimeout))
          return pluginInstance.Status == PluginStatus.Connected;

        if (pluginInstance.Status == PluginStatus.Stopped)
        {
          LogTo.Error($"{pluginInstance.Denomination.CapitalizeFirst()} {packageName} stopped unexpectedly.");
          pluginInstance.ConnectedEvent.Set();
          return false;
        }
      }
      catch (Exception ex)
      {
        LogTo.Error(ex, $"An error occured while starting {pluginInstance}");
        return false;
      }

      try
      {
        LogTo.Warning(
          $"{pluginInstance.ToString().CapitalizeFirst()} failed to connect under {PluginConnectTimeout}ms. Attempting to kill process");

        pluginInstance.Process.Refresh();

        if (pluginInstance.Process.HasExited)
        {
          LogTo.Warning($"{pluginInstance.ToString().CapitalizeFirst()} has already exited");
          return false;
        }

        pluginInstance.Process.Kill();
      }
      catch (RemotingException ex)
      {
        LogTo.Warning(ex, $"StartPlugin '{pluginInstance} failed.");
      }
      catch (Exception ex)
      {
        LogTo.Error(ex, $"An error occured while starting {pluginInstance}");
      }
      finally
      {
        try
        {
          using (await pluginInstance.Lock.LockAsync())
            OnPluginStopped(pluginInstance);
        }
        catch (Exception ex)
        {
          LogTo.Error(ex, "Exception thrown while calling OnPluginStopped");
        }
      }

      return false;
    }

    public async Task<bool> StopPlugin(TPluginInstance pluginInstance)
    {
      try
      {
        using (await pluginInstance.Lock.LockAsync())
          switch (pluginInstance.Status)
          {
            case PluginStatus.Stopping:
            case PluginStatus.Stopped:
              return true;

            default:
              OnPluginStopping(pluginInstance);

              try
              {
                pluginInstance.Plugin?.Dispose();
              }
              catch (RemotingException ex)
              {
                LogTo.Warning(
                  ex, $"Failed to gracefully stop {pluginInstance}' failed.");
              }
              catch (Exception ex)
              {
                LogTo.Error(ex, $"An exception occured while gracefully stopping {pluginInstance}.");
              }

              break;
          }

        try
        {
          if (pluginInstance.Process is null)
          {
            LogTo.Warning(
              $"pluginInstance.Process is null. Unable to monitor stop or kill {pluginInstance}.");
            return false;
          }

          if (await Task.Run(() => pluginInstance.Process.WaitForExit(PluginStopTimeout)))
            return true;

          pluginInstance.Process.Refresh();

          if (pluginInstance.Process.HasExited)
            return true;

          LogTo.Warning(
            $"{pluginInstance.ToString().CapitalizeFirst()} didn't shut down gracefully after {PluginStopTimeout}ms. Attempting to kill process");

          pluginInstance.Process.Kill();
          
          return true;
        }
        catch (Exception ex)
        {
          LogTo.Error(ex, $"An exception occured while killing {pluginInstance}");
        }
      }
      catch (RemotingException ex)
      {
        LogTo.Warning(ex, $"StopPlugin {pluginInstance?.ToString()}' failed.");
      }
      finally
      {
        using (await pluginInstance.Lock.LockAsync())
          try
          {
            OnPluginStopped(pluginInstance);
          }
          catch (Exception ex)
          {
            LogTo.Error(ex, "Exception thrown while calling OnPluginStopped");
          }
      }

      return false;
    }

    #endregion
  }
}
