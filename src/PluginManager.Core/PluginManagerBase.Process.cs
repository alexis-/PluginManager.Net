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
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Anotar.Custom;
using CommandLine;
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

        // Build command line
        var cmdLineParams = new PluginHostParameters
        {
          PluginHostTypeAssemblyName  = GetPluginHostTypeAssemblyName(pluginInstance),
          PluginHostTypeQualifiedName = GetPluginHostTypeQualifiedName(pluginInstance),
          PackageName                 = packageName,
          HomePath                    = pluginPackage.HomeDir.FullPath,
          SessionString               = pluginInstance.Guid.ToString(),
          ChannelName                 = IpcServerChannelName,
          ManagerProcessId            = Process.GetCurrentProcess().Id,
          IsDevelopment               = pluginInstance.IsDevelopment,
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

        pluginInstance.Process.EnableRaisingEvents = true;
        pluginInstance.Process.BeginErrorReadLine();
        pluginInstance.Process.Exited += async (o, e) =>
        {
          using (await pluginInstance.Lock.LockAsync())
            OnPluginStopped(pluginInstance);
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
        using (await pluginInstance.Lock.LockAsync())
          OnPluginStopped(pluginInstance);
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
          OnPluginStopped(pluginInstance);
      }

      return false;
    }

    #endregion
  }
}
