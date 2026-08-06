using System;
using System.IO;
using System.Linq;
using System.Threading;
using ConsoleAppFramework;

namespace Deneblab.AbcVersionCmd;

internal class ProjectsCommands
{
    /// <summary>List all configured projects from .abcversion.json</summary>
    /// <param name="cancellationToken"></param>
    /// <param name="path">Path to git directory with configuration file (.abcversion.json)</param>
    [Command("")]
    public void Root(
        CancellationToken cancellationToken,
        string path = default
    )
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        var configPath = Path.Combine(path, ".abcversion.json");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"No .abcversion.json found in {Path.GetFullPath(path)}");
            Console.Error.WriteLine("Run 'abcversion init' to create one.");
            Environment.ExitCode = 1;
            return;
        }

        var config = ConfigReader.Read(path);
        if (config == null)
        {
            Console.Error.WriteLine("Failed to read .abcversion.json");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"BaseVersion: {config.BaseVersion ?? "(not set)"}");

        if (config.Projects.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("No projects configured. Using single-project mode.");
            Console.WriteLine("Add a \"Projects\" section to .abcversion.json for multi-project support.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Projects:");

        var maxName = config.Projects.Keys.Max(k => k.Length);
        var maxVersion = config.Projects.Values.Max(p => (p.BaseVersion ?? "").Length);

        foreach (var (name, project) in config.Projects)
        {
            var paddedName = name.PadRight(maxName);
            var version = (project.BaseVersion ?? "???").PadRight(maxVersion);
            var projectPath = project.Path ?? ".";
            Console.WriteLine($"  {paddedName}   BaseVersion: {version}   Path: {projectPath}");
        }
    }
}
