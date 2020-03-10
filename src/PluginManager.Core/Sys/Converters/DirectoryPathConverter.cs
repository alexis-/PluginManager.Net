using System;
using System.ComponentModel;
using Extensions.System.IO;
using Newtonsoft.Json;

namespace PluginManager.Sys.Converters
{
  /// <summary>
  /// Converts <see cref="DirectoryPath"/> to json and vice-versa
  /// </summary>
  [EditorBrowsable(EditorBrowsableState.Never)]
  internal class DirectoryPathConverter : JsonConverter<DirectoryPath>

  {
    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, DirectoryPath value, JsonSerializer serializer)
    {
      writer.WriteValue(value.FullPath);
    }

    /// <inheritdoc />
    public override DirectoryPath ReadJson(JsonReader reader, Type objectType, DirectoryPath existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
      return (string)reader.Value;
    }
  }
}
