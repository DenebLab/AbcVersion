using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deneblab.AbcVersionCmd;

internal static class ConfigReader
{
    public static CliConfig Read(string repoPath)
    {
        var configPath = Path.Combine(repoPath, ".abcversion.json");
        if (!File.Exists(configPath))
            return null;

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize(json, AbcVersionCmdJsonContext.Default.CliConfig);
    }

    public static List<string> GetProjectNames(string repoPath)
    {
        var config = Read(repoPath);
        if (config?.Projects == null || config.Projects.Count == 0)
            return [];

        return new List<string>(config.Projects.Keys);
    }
}

internal class CliConfig
{
    [JsonPropertyName("BaseVersion")]
    public string BaseVersion { get; set; }

    [JsonPropertyName("Projects")]
    public Dictionary<string, CliProject> Projects { get; set; } = new();
}

internal class CliProject
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Path")]
    public string Path { get; set; }

    [JsonPropertyName("BaseVersion")]
    public string BaseVersion { get; set; }
}
