# PluginManager.Net

[![NuGet](https://img.shields.io/nuget/v/PluginManager.Core.svg)](https://www.nuget.org/packages/PluginManager.Core/)
[![NuGet](https://img.shields.io/nuget/v/PluginManager.Interop.svg)](https://www.nuget.org/packages/PluginManager.Interop/)
[![NuGet](https://img.shields.io/nuget/v/PluginManager.PluginHost.svg)](https://www.nuget.org/packages/PluginManager.PluginHost/)

**PluginManager.Net** is a comprehensive framework for .NET applications designed to discover, install, update, and manage plugins from NuGet feeds. It provides a robust architecture for running plugins in isolated processes, ensuring the stability and security of the host application while enabling seamless communication via IPC.

## Key Features

- **Dynamic Plugin Management**: Install, update, and uninstall plugins from any NuGet-compatible repository.
- **Process Isolation**: Plugins run in their own dedicated processes (`PluginHost.exe`), preventing a faulty plugin from crashing the main application.
- **Robust IPC**: Built-in Inter-Process Communication using .NET Remoting allows for type-safe communication between the host application and plugins, as well as between plugins themselves.
- **Automatic Dependency Management**: Automatically resolves and installs NuGet dependencies for each plugin.
- **Development Mode**: Supports loading "development plugins" directly from a folder for easy debugging and iteration, bypassing the need to pack and publish to NuGet during development.
- **Extensible Architecture**: Uses generics and abstract base classes to allow for deep customization, including custom metadata (`TMeta`) for plugins and custom core services (`ICore`) shared with plugins.
- **Lifecycle Management**: Full control over starting, stopping, and monitoring plugin processes.
- **Service Discovery**: Plugins can publish and consume services from other running plugins.

## Architecture

The framework is composed of three main components, delivered as separate NuGet packages:

- **`PluginManager.Core`**: The heart of the framework, meant to be integrated into your host application. It contains the `PluginManagerBase` class which you extend to manage plugins, and the `PluginPackageManager` which handles all NuGet interactions.
- **`PluginManager.PluginHost`**: A lightweight, standalone executable responsible for loading and running a single plugin in its own sandboxed process and `AppDomain`. This is the key to process isolation.
- **`PluginManager.Interop`**: A shared library containing the contracts (`IPluginManager`, `IPluginBase`) and base classes (`PluginBase`) necessary for IPC communication. Both your host application and your plugins will reference this.

The typical workflow is as follows:
1.  The **Host Application** (using `PluginManager.Core`) decides to start a plugin.
2.  It launches a new instance of **`PluginHost.exe`**, passing it information about which plugin assembly to load.
3.  `PluginHost.exe` creates a new `AppDomain`, loads the plugin assembly, and instantiates the plugin's main class (which implements `IPluginBase` from `PluginManager.Interop`).
4.  The plugin process connects back to the Host Application via an IPC channel to establish communication.
5.  The Host and Plugin can now exchange calls and data through their shared interfaces.

## Getting Started

Follow these steps to integrate PluginManager.Net into your application.

### 1. Setting Up Your Host Application

First, you need to set up the central manager in your main application (e.g., WPF, WinForms, Console).

**A. Install NuGet Package:**
Install the core package into your main application project:
```powershell
Install-Package PluginManager.Core
```
This will also bring in `PluginManager.PluginHost` as a dependency, which automatically adds `PluginHost.exe` to your output directory.

**B. Create Your Plugin Manager Implementation:**
Create a class that inherits from `PluginManagerBase`. You'll need to define several types via generics:
- `TParent`: The class itself.
- `TPluginInstance`: A class representing a running plugin instance.
- `TMeta`: A class to hold custom metadata for your plugins.
- `ICustomPluginManager`: The IPC interface for your manager (usually just `IPluginManager<ICore>`).
- `ICore`: The interface for a service you want to expose to all plugins.
- `IPlugin`: The base interface for your plugins (usually just `IPluginBase`).

```csharp
// Example: A simple metadata class
public class MyPluginMetadata
{
  public string Author { get; set; }
  public string Description { get; set; }
}

// Example: Your core service interface to share with plugins
public interface IMyCoreApi
{
  string GetHostApplicationVersion();
  void ShowNotification(string message);
}

// Example: A class to represent a running plugin instance
public class MyPluginInstance : PluginInstanceBase<MyPluginInstance, MyPluginMetadata, IPluginBase>
{
    public MyPluginInstance(LocalPluginPackage<MyPluginMetadata> package) : base(package) { }
    
    // You must implement these abstract members
    public override bool IsEnabled { get; set; } = true; // Or load from config
    public override bool Equals(MyPluginInstance other) => base.Equals(other);
}
```

Now, create your main manager class by implementing the abstract members of `PluginManagerBase`.

```csharp
using PluginManager;
using PluginManager.Contracts;
using PluginManager.Interop.Contracts;
using PluginManager.Logger;
using PluginManager.PackageManager.Models;
using System.IO;
using System.Threading;

public class MyPluginManager : PluginManagerBase<MyPluginManager, MyPluginInstance, MyPluginMetadata, IPluginManager<IMyCoreApi>, IMyCoreApi, IPluginBase>
{
    // Implement the abstract properties and methods

    // 1. Define file and folder locations
    public override IPluginLocations Locations => new MyPluginLocations();

    // 2. Provide a log adapter to pipe logs to your app's logger
    public override ILogAdapter LogAdapter => new MyLogAdapter();

    // 3. Provide the UI synchronization context (critical for WPF/WinForms)
    public override SynchronizationContext UISynchronizationContext => SynchronizationContext.Current;

    // 4. Provide an instance of your core API for plugins to consume
    public override IMyCoreApi GetCoreInstance() => new MyCoreApiImplementation();

    // 5. Tell the manager how to create an instance of your PluginInstance class
    public override MyPluginInstance CreatePluginInstance(LocalPluginPackage<MyPluginMetadata> package)
    {
        return new MyPluginInstance(package);
    }
    
    // These abstract methods define which type from which assembly is loaded in PluginHost.exe
    // For a standard setup, you only need PluginManager.Interop.
    public override string GetPluginHostTypeAssemblyName(MyPluginInstance pluginInstance)
    {
        return "PluginManager.Interop";
    }
    public override NuGetVersion GetPluginHostTypeAssemblyMinimumVersion(MyPluginInstance pluginInstance)
    {
        // Set to the version of PluginManager.Interop you are using
        return NuGetVersion.Parse("0.2.1.52"); 
    }
    public override string GetPluginHostTypeQualifiedName(MyPluginInstance pluginInstance)
    {
        // This is the default implementation provided in PluginManager.Interop
        // You would only change this if you created your own PluginHostBase implementation.
        return "PluginManager.Interop.PluginHost.DefaultPluginHost"; 
    }

    // (Other methods...)
}

// Helper class for locations
public class MyPluginLocations : IPluginLocations
{
    private static readonly string AppRoot = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    public DirectoryPath PluginDir => Path.Combine(AppRoot, "Plugins");
    public DirectoryPath PluginHomeDir => Path.Combine(PluginDir, "Home");
    public DirectoryPath PluginPackageDir => Path.Combine(PluginDir, "Packages");
    public DirectoryPath PluginDevelopmentDir => Path.Combine(PluginDir, "Dev");
    public FilePath PluginHostExeFile => Path.Combine(AppRoot, "PluginHost.exe");
    public FilePath PluginConfigFile => Path.Combine(PluginDir, "plugins.json");
}
```

**C. Initialize the Manager:**
In your application's startup logic, create an instance and initialize it.

```csharp
public async Task AppStartup()
{
    var manager = new MyPluginManager();
    await manager.Initialize(); // Scans for local plugins and starts enabled ones

    // The manager is now ready to use
}
```

### 2. Creating Your First Plugin

Plugins are simple class libraries that reference `PluginManager.Interop`.

**A. Install NuGet Package:**
In your new plugin project (Class Library .NET Framework), install the interop package:
```powershell
Install-Package PluginManager.Interop
```

**B. Create Your Plugin Class:**
Inherit from `PluginBase` and implement the abstract members. `ICore` should match the interface you defined in your host application setup.

```csharp
using PluginManager.Interop.Plugins;
using System;

// Use the same IMyCoreApi interface defined in the host
public class MyFirstPlugin : PluginBase<MyFirstPlugin, IPluginBase, IMyCoreApi>
{
    public MyFirstPlugin() : base() { }

    // Implement abstract members
    public override string Name => "My Awesome First Plugin";

    protected override void LogInformation(string message) => Console.WriteLine($"[INFO] {message}");
    protected override void LogError(Exception ex, string message) => Console.WriteLine($"[ERROR] {message}: {ex.Message}");

    // This method is called after the plugin is connected and dependencies are injected
    protected override void OnPluginInitialized()
    {
        LogInformation($"{Name} has started!");

        // You can now access the host application's API
        var hostVersion = Service.GetHostApplicationVersion();
        LogInformation($"Connected to host version: {hostVersion}");
        
        Service.ShowNotification("Hello from MyFirstPlugin!");
    }
}
```

**C. Package Your Plugin:**
Package your class library as a NuGet package. The `PluginManager.Net` will find and install it from a NuGet feed.

## Common Operations

Here are examples of how to use the plugin manager from your host application.

```csharp
var manager = new MyPluginManager();
await manager.Initialize();

// --- Search for new plugins ---
// (You may need to implement IPluginRepositoryService for custom discovery)
var searchResults = await manager.SearchPluginsAsync("MyPlugin", enablePrerelease: true);
var pluginToInstall = searchResults.FirstOrDefault();


// --- Install a plugin ---
if (pluginToInstall != null)
{
    bool installSuccess = await manager.InstallPluginAsync(pluginToInstall);
    if (installSuccess)
    {
        Console.WriteLine("Plugin installed successfully!");
    }
}

// --- List and start an installed plugin ---
var installedPluginInstance = manager.AllPlugins.FirstOrDefault(p => p.Package.Id == "MyFirstPlugin");
if (installedPluginInstance != null && installedPluginInstance.Status == PluginStatus.Stopped)
{
    bool startSuccess = await manager.StartPlugin(installedPluginInstance);
    if (startSuccess)
    {
        Console.WriteLine("Plugin started!");
    }
}

// --- Update a plugin ---
var pluginToUpdate = manager.AllPlugins.FirstOrDefault(p => p.Package.HasPendingUpdates);
if (pluginToUpdate != null)
{
    await manager.UpdatePluginAsync(pluginToUpdate.Package.Id);
}


// --- Uninstall a plugin ---
var pluginToUninstall = manager.AllPlugins.FirstOrDefault(p => p.Package.Id == "SomeOldPlugin");
if (pluginToUninstall != null)
{
    await manager.UninstallPluginAsync(pluginToUninstall);
}
```

## The Projects

- **PluginManager.Core**: The main library for the host application. Provides plugin lifecycle, package management, and IPC server logic.
- **PluginManager.Interop**: A contract library for communication. Contains interfaces (`IPluginBase`, `IPluginManager`) and base classes (`PluginBase`) shared between the host and plugins.
- **PluginManager.PluginHost**: The executable that hosts plugins in isolated processes. It's automatically included when you reference `PluginManager.Core`.
- **Shared Projects**: Contain shared constants and code used across the solution.

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.