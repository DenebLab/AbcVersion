using System;
using System.IO;
using System.Threading;
using ConsoleAppFramework;
using Deneblab.AbcVersion;

namespace Deneblab.AbcVersionCmd;

internal class InfoCommands
{
    /// <summary>Show diagnostic information about the current repository and version resolution</summary>
    /// <param name="cancellationToken"></param>
    /// <param name="path">Path to git directory with configuration file (.abcversion.json)</param>
    /// <param name="project">Project name from configuration file (.abcversion.json)</param>
    [Command("")]
    public void Root(
        CancellationToken cancellationToken,
        string path = default,
        string project = default
    )
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        project = string.IsNullOrEmpty(project) ? "." : project;

        var configPath = Path.Combine(path, ".abcversion.json");
        var configExists = File.Exists(configPath);

        var abcVersion = AbcVersionFactory
            .CreateBuilder()
            .SetRepositoryRoot(path)
            .Build(project);

        Console.WriteLine($"Repository:    {Path.GetFullPath(path)}");
        Console.WriteLine($"Config:        {(configExists ? configPath : "(not found)")}");
        Console.WriteLine($"Branch:        {abcVersion.GitBranch}");
        Console.WriteLine($"Git SHA:       {abcVersion.GitSha}");
        Console.WriteLine($"SemVersion:    {abcVersion.SemVersion}");
        Console.WriteLine($"Major:         {abcVersion.Major}");
        Console.WriteLine($"Minor:         {abcVersion.Minor}");
        Console.WriteLine($"Patch:         {abcVersion.Patch}");

        if (!string.IsNullOrEmpty(abcVersion.PreRelease))
            Console.WriteLine($"PreRelease:    {abcVersion.PreRelease}");

        Console.WriteLine($"Assembly:      {abcVersion.AssemblyVersion}");
        Console.WriteLine($"File:          {abcVersion.FileVersion}");
        Console.WriteLine($"Machine:       {abcVersion.Machine}");
        Console.WriteLine($"DateTime:      {abcVersion.DateTime:s}Z");

        if (project != ".")
        {
            Console.WriteLine($"Project:       {project}");
            var config = ConfigReader.Read(path);
            if (config?.Projects != null && config.Projects.TryGetValue(project, out var proj))
            {
                Console.WriteLine($"ProjectPath:   {proj.Path ?? "."}");
                Console.WriteLine($"BaseVersion:   {proj.BaseVersion ?? "(not set)"}");
            }
        }
        else if (configExists)
        {
            var config = ConfigReader.Read(path);
            if (config != null)
                Console.WriteLine($"BaseVersion:   {config.BaseVersion ?? "(not set)"}");
        }
    }
}
