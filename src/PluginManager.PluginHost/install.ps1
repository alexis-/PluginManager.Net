param($installPath, $toolsPath, $package, $project)

$project.ProjectItems.Item('PluginHost.exe').Properties.Item("CopyToOutputDirectory").Value = [int]2;