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
    /// <param name="scope">Subdirectory (from the repository root) to narrow the commit count to</param>
    [Command("")]
    public void Root(
        CancellationToken cancellationToken,
        string path = default,
        string project = default,
        string scope = default
    )
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        project = string.IsNullOrEmpty(project) ? "." : project;

        ScopeGuard.RejectScopeWithProject(scope, project);

        var abcVersion = AbcVersionFactory
            .CreateBuilder()
            .SetRepositoryRoot(path)
            .SetScope(scope)
            .Build(project);

        // Reported from what the calculation resolved, not from the path passed in: the two differ
        // whenever --path names a subdirectory, and reporting the argument claimed there was no
        // configuration while its BaseVersion was in use.
        var repositoryRoot = abcVersion.RepositoryRoot ?? Path.GetFullPath(path);
        var configPath = abcVersion.ConfigPath;
        var configExists = configPath != null;

        Console.WriteLine($"Repository:    {repositoryRoot}");
        Console.WriteLine($"Config:        {configPath ?? "(not found)"}");
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

        if (string.IsNullOrEmpty(scope) == false)
            Console.WriteLine($"Scope:         {scope}");

        if (project != ".")
        {
            Console.WriteLine($"Project:       {project}");
            var config = ConfigReader.Read(repositoryRoot);
            if (config?.Projects != null && config.Projects.TryGetValue(project, out var proj))
            {
                Console.WriteLine($"ProjectPath:   {proj.Path ?? "."}");
                Console.WriteLine($"BaseVersion:   {proj.BaseVersion ?? "(not set)"}");
            }
        }
        else if (configExists)
        {
            var config = ConfigReader.Read(repositoryRoot);
            if (config != null)
                Console.WriteLine($"BaseVersion:   {config.BaseVersion ?? "(not set)"}");
        }
    }
}
