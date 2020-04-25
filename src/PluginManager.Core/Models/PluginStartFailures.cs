using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginManager.Models
{
  /// <summary>
  /// Defines the reason why a plugin couldn't be started
  /// </summary>
  public enum PluginStartFailure
  {
    InteropAssemblyNotFound,
    InteropAssemblyInvalidVersionString,
    InteropAssemblyOutdated,
    ProcessDidNotStart,
    ProcessDidNotConnect,
    Unknown,
  }
}
