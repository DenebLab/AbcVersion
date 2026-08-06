using System.Text.Json;
using System.Text.Json.Serialization;
using Deneblab.StashLock.Cli.Common.Simple;

namespace Deneblab.AbcVersion.Internal;

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(AbcVersionConfig))]
[JsonSerializable(typeof(SimpleEnvResult))]
internal partial class AbcVersionJsonContext : JsonSerializerContext
{
}
