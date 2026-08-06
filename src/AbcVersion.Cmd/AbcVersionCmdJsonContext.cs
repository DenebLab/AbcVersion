using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deneblab.AbcVersionCmd;

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(CliConfig))]
[JsonSerializable(typeof(Deneblab.AbcVersion.AbcVersion))]
internal partial class AbcVersionCmdJsonContext : JsonSerializerContext
{
}
