using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deneblab.StashLock.Cli.Common.Simple;
using Microsoft.Extensions.Logging;

namespace Deneblab.AbcVersion.Internal;

internal sealed class AbcVersionCreator
{
    private const string _MAIN_PROJECT_MARKER = AbcVersionConsts.MAIN_PROJECT_MARKER;
    private readonly ILogger _log;
    private readonly string _root;
    private readonly GitTool _tool;

    public AbcVersionCreator(ILogger log, string root, GitTool tool)
    {
        _log = log;
        _root = root;
        _tool = tool;
    }


    private AbcVersionMainModel GetMainModel()
    {
        var config = GetConfig(_root);
        var m = new AbcVersionMainModel
        {
            Snapshots = config.Snapshots
        };
        var mainProject = config.Projects.Values.FirstOrDefault(x => x.Name == _MAIN_PROJECT_MARKER);
        if (mainProject == null)
        {
            mainProject = new Project
            {
                Name = _MAIN_PROJECT_MARKER,
                Path = _MAIN_PROJECT_MARKER,
                BaseVersion = config.BaseVersion
            };

            foreach (var branch in config.Branches)
            {
                var name = branch.Key;
                var startPoint = new StartPoint
                {
                    ParentSha = RequireBranchField(branch.Value.ParentSha, "ParentSha", name),
                    Version = RequireBranchField(branch.Value.Version, "Version", name)
                };
                var newBranch = new Branch
                    { Name = name, StartPoint = startPoint, BaseVersion = mainProject.BaseVersion };
                mainProject.Branches.Add(name, newBranch);
            }
        }
        else
        {
            if (mainProject.Path != _MAIN_PROJECT_MARKER) throw new ArgumentException("Main project path must be '.'");
        }

        m.Projects.Add(mainProject.Name, mainProject);

        foreach (var project in config.Projects.Where(x => x.Key != _MAIN_PROJECT_MARKER))
        {
            var p = new Project
            {
                Name = project.Key,
                Path = project.Value.Path,
                BaseVersion = project.Value.BaseVersion
            };
            foreach (var branch in project.Value.Branches)
            {
                var name = branch.Key;
                var startPoint = new StartPoint
                {
                    ParentSha = RequireBranchField(branch.Value.StartPoint?.ParentSha, "StartPoint.ParentSha", name, project.Key),
                    Version = RequireBranchField(branch.Value.StartPoint?.Version, "StartPoint.Version", name, project.Key)
                };
                var newBranch = new Branch { Name = name, StartPoint = startPoint };
                p.Branches.Add(name, newBranch);
            }

            m.Projects.Add(p.Name, p);
        }

        return m;
    }

    private static string RequireBranchField(string value, string fieldName, string branchName, string projectName = null)
    {
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

        var location = projectName == null
            ? $"branch '{branchName}'"
            : $"project '{projectName}', branch '{branchName}'";
        throw new ArgumentException(
            $"{fieldName} is required in .abcversion.json for {location} — it cannot be null or empty.");
    }

    internal static AbcVersionConfig GetConfig(string pathToRepository)
    {
       
       var path = Path.Combine(pathToRepository, ".abcversion.json");
        if (File.Exists(path) == false) return new AbcVersionConfig();

        var json = File.ReadAllText(path);
        var o = JsonSerializer.Deserialize(json, AbcVersionJsonContext.Default.AbcVersionConfig);
        return o;
    }


    public SemVersion GetSemVersion(
        AbcVersionMainModel config,
        string projectName,
        bool useMainProjectIfFail = false)
    {
        var currentSha = _tool.GetHash();
        if (config.Snapshots.TryGetValue(currentSha, out var snapshotVersion))
            return SemVersion.Parse(snapshotVersion);


        if (config.Projects.ContainsKey(projectName) == false)
        {
            if (useMainProjectIfFail == false)
                throw new ArgumentException($"Cannot find project: {projectName} in config file");

            projectName = _MAIN_PROJECT_MARKER;
        }


        var projectExists = config
            .Projects.TryGetValue(projectName, out var project);


        if (projectExists == false)
            throw new ArgumentException($"Cannot find project: {projectName} in config file");

        var baseSemVersion = SemVersion.Parse(project.BaseVersion);
        var currentBranch = _tool.GetBranch();

        _log.LogTrace($"ProjectName; {projectName}; ProjectPath; {project.Path}; " +
                      $"CurrentBranch: {currentBranch}");

        if (project.Branches.TryGetValue(currentBranch, out var configBranch))
        {
            var startSha = configBranch.StartPoint.ParentSha;
            var startVersion = configBranch.StartPoint.Version;
            var exists = _tool.GetShaExists(currentBranch, startSha);
            if (exists == false) throw new ArgumentException($"Cannot find sha: {startSha} in branch: {currentBranch}");

            var firstParentNumber = _tool.GetCommitNumberForPath(project.Path, startSha);
            var sem = SemVersion.Parse(startVersion);
            var patchNewValue = sem.Patch + firstParentNumber - 1;
            var pathNewValue2 = patchNewValue < 0 ? 0 : patchNewValue;
            var newSem = new SemVersion(sem.Major, sem.Minor, pathNewValue2);
            return newSem;
        }
        else
        {
            var firstParentNumber = _tool.GetCommitNumberForPath(project.Path);
            var newSem = new SemVersion(baseSemVersion.Major, baseSemVersion.Minor, firstParentNumber);
            return newSem;
        }
    }


    public AbcVersion Build(string projectNameFromConfig, AbcVersionOptions options)
    {
        var mainModel = GetMainModel();
        var semVersion = GetSemVersion(mainModel, projectNameFromConfig);
        var data = _tool.GetAllGitData();
        return new AbcVersion(semVersion, data, options.DateTime);
    }

    public static string GetRoot(SimpleEnvResult env, AbcVersionOptions options)
    {
        if (IsGitRoot(options.PathToRepository)) return options.PathToRepository;
        var r = FindGitDirectory(env.AppRoot);
        return r;


        bool IsGitRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var gitPath = Path.Combine(path, ".git");
            return Directory.Exists(gitPath);
        }

        static string FindGitDirectory(string startPath)
        {
            var directory = startPath;

            while (directory != null)
            {
                var gitPath = Path.Combine(directory, ".git");

                if (Directory.Exists(gitPath))
                {
                    return directory;
                }

                // Move up to the parent directory
                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }
    }
}