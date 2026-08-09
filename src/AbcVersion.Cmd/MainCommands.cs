using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ConsoleAppFramework;
using Deneblab.AbcVersion;

namespace Deneblab.AbcVersionCmd;

public class MainCommands
{
    /// <summary>Get version information for the current repository</summary>
    /// <param name="cancellationToken"></param>
    /// <param name="path">Path to git directory with configuration file (.abcversion.json)</param>
    /// <param name="project">Project name from configuration file (.abcversion.json)</param>
    /// <param name="scope">Subdirectory (from the repository root) to narrow the commit count to</param>
    /// <param name="property">-p, Get only one property value (e.g. semversion, major, gitsha)</param>
    [Command("")]
    public void Root(
        CancellationToken cancellationToken,
        string path = default,
        string project = default,
        string scope = default,
        string property = default
    )
    {
        var options = new JsonSerializerOptions(AbcVersionCmdJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var jsonContext = new AbcVersionCmdJsonContext(options);

        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        project = string.IsNullOrEmpty(project) ? "." : project;

        ScopeGuard.RejectScopeWithProject(scope, project);

        Deneblab.AbcVersion.AbcVersion abcVersion;
        try
        {
            abcVersion = AbcVersionFactory
                .CreateBuilder()
                .SetRepositoryRoot(path)
                .SetScope(scope)
                .Build(project);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Cannot find project"))
        {
            // Read the configuration from the root the calculation resolved, not from the argument:
            // with --path naming a subdirectory the argument holds no config, and the hint that
            // names the configured projects would silently go missing.
            var configRoot = AbcVersionFactory.ResolveRepositoryRoot(path) ?? path;
            var available = ConfigReader.GetProjectNames(configRoot);
            if (available.Count > 0)
                throw new ArgumentException(
                    $"Project '{project}' not found in .abcversion.json. Available projects: {string.Join(", ", available)}");
            throw;
        }

        var json = JsonSerializer.Serialize(abcVersion, jsonContext.AbcVersion);
        if (string.IsNullOrEmpty(property))
        {
            Console.Write(json);
        }
        else
        {
            var node = JsonNode.Parse(json).AsObject();
            var match = node.FirstOrDefault(kv => string.Equals(kv.Key, property, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null)
            {
                var availableProps = string.Join(", ", node.Select(kv => kv.Key).OrderBy(k => k));
                throw new KeyNotFoundException(
                    $"Property '{property}' not found. Available: {availableProps}");
            }
            Console.Write(match.Value?.ToString());
        }
    }
}
