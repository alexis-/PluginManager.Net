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
// Modified On:  2020/02/25 12:18
// Modified By:  Alexis

#endregion




using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PluginManager.Interop.Contracts;

namespace PluginManager.Interop.PluginHost
{
  public abstract partial class PluginHostBase<ICore>
  {
    #region Properties & Fields - Non-Public

    protected          string        PluginInterfaceFullName { get; } = typeof(IPluginBase).FullName;
    protected abstract HashSet<Type> CoreInterfaceTypes      { get; }
    protected abstract HashSet<Type> PluginMgrInterfaceTypes { get; }

    #endregion




    #region Methods

    private IPluginBase LoadAssembliesAndCreatePluginInstance(
      string pluginEntryAssembly)
    {
      var pluginAssembly = Assembly.LoadFrom(pluginEntryAssembly);

      return CreatePluginInstance(pluginAssembly);
    }

    private IPluginBase CreatePluginInstance(Assembly pluginAssembly)
    {
      Type pluginType = FindPluginType(pluginAssembly);

      if (pluginType == null)
        return null;

      return (IPluginBase)Activator.CreateInstance(pluginType);
    }

    private Type FindPluginType(Assembly pluginAssembly)
    {
      var exportedTypes = pluginAssembly.GetExportedTypes();

      return exportedTypes.FirstOrDefault(t => t.IsAbstract == false && t.GetInterface(PluginInterfaceFullName) != null);
    }

    private bool InjectPropertyDependencies(IPluginBase           plugin,
                                            ICore                 coreInst,
                                            IPluginManager<ICore> pluginMgr,
                                            Guid                  sessionGuid,
                                            bool                  isDevelopment)
    {
      bool coreSet = false;
      bool mgrSet  = false;
      bool guidSet = false;
      var  type    = plugin.GetType();

      while (type != null && type != typeof(IPluginBase))
      {
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var prop in props)
          if (CoreInterfaceTypes.Contains(prop.PropertyType) && prop.CanWrite)
          {
            prop.SetValue(plugin, coreInst /*Convert.ChangeType(coreInst, prop.PropertyType)*/);
            coreSet = true;
          }

          else if (PluginMgrInterfaceTypes.Contains(prop.PropertyType) && prop.CanWrite)
          {
            prop.SetValue(plugin, pluginMgr /*Convert.ChangeType(pluginMgr, prop.PropertyType)*/);
            mgrSet = true;
          }

          else if (prop.PropertyType == typeof(Guid) && prop.CanWrite)
          {
            prop.SetValue(plugin, sessionGuid);
            guidSet = true;
          }

          else if (prop.PropertyType == typeof(bool) && prop.Name is "IsDevelopmentPlugin" && prop.CanWrite)
          {
            prop.SetValue(plugin, isDevelopment);
          }

        type = type.BaseType;
      }

      return coreSet && mgrSet && guidSet;
    }

    private Assembly DevelopmentPluginAssemblyResolver(object sender, ResolveEventArgs e)
    {
      var assembly = AppDomain.CurrentDomain
                              .GetAssemblies()
                              .FirstOrDefault(a => a.FullName == e.Name);

      if (assembly != null)
        return assembly;

      var assemblyName = e.Name.Split(',').First() + ".dll";
      var homePath     = AppDomain.CurrentDomain.BaseDirectory;
      var assemblyPath = Path.Combine(homePath, assemblyName);

      return File.Exists(assemblyPath) == false
        ? null
        : Assembly.LoadFrom(assemblyPath);
    }

    #endregion
  }
}
