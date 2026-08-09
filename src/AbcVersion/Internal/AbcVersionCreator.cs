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

    /// <summary>
    ///     Full path to the configuration file in use, or <c>null</c> when there is none. Resolved
    ///     against the repository root the search settled on, which is what makes it safe to report:
    ///     a caller-supplied subdirectory would answer "(not found)" for a config that was used.
    /// </summary>
    internal static string GetConfigPath(string pathToRepository)
    {
        var path = Path.Combine(pathToRepository, ".abcversion.json");
        return File.Exists(path) ? path : null;
    }

    internal static AbcVersionConfig GetConfig(string pathToRepository)
    {

       var path = GetConfigPath(pathToRepository);
        if (path == null) return new AbcVersionConfig();

        var json = File.ReadAllText(path);
        var o = JsonSerializer.Deserialize(json, AbcVersionJsonContext.Default.AbcVersionConfig);
        return o;
    }


    public SemVersion GetSemVersion(
        AbcVersionMainModel config,
        string projectName,
        AbcVersionOptions options = null,
        bool useMainProjectIfFail = false)
    {
        var currentSha = _tool.GetHash();
        if (config.Snapshots.TryGetValue(currentSha, out var snapshotVersion))
            return SemVersion.Parse(snapshotVersion);

        // A snapshot pins the version for a specific commit and deliberately wins over every other
        // rule, scope included.
        var scopePath = options?.ScopePath;
        if (string.IsNullOrWhiteSpace(scopePath) == false)
            return GetSemVersionForScope(config, projectName, scopePath);


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


    /// <summary>
    ///     Versions a subtree that has no <c>Projects</c> entry: the base version comes from the
    ///     configuration root, and the commit count is narrowed to <paramref name="scopePath" />.
    /// </summary>
    private SemVersion GetSemVersionForScope(AbcVersionMainModel config, string projectName, string scopePath)
    {
        // Scope and project both narrow the commit count. Any precedence rule would leave one of
        // them silently ignored, so the combination is refused instead.
        if (projectName != _MAIN_PROJECT_MARKER)
            throw new ArgumentException(
                $"Scope and project cannot be combined (scope: '{scopePath}', project: '{projectName}'). " +
                "Both narrow the commit count — pick one.");

        var scope = NormalizeScope(scopePath);
        var mainProject = config.Projects[_MAIN_PROJECT_MARKER];
        var currentBranch = _tool.GetBranch();

        _log.LogTrace($"Scope; {scope}; CurrentBranch: {currentBranch}");

        // A scoped run honours the branch start point when the current branch has one, so that
        // --scope agrees with the way this repository already versions the branch.
        if (mainProject.Branches.TryGetValue(currentBranch, out var configBranch))
        {
            var startSha = configBranch.StartPoint.ParentSha;
            if (_tool.GetShaExists(currentBranch, startSha) == false)
                throw new ArgumentException($"Cannot find sha: {startSha} in branch: {currentBranch}");

            var countSinceStart = _tool.GetCommitNumberForPath(scope, startSha);
            RequireScopeMatchesCommits(countSinceStart, scope, scopePath);

            var startSem = SemVersion.Parse(configBranch.StartPoint.Version);
            var patch = startSem.Patch + countSinceStart - 1;
            return new SemVersion(startSem.Major, startSem.Minor, patch < 0 ? 0 : patch);
        }

        var count = _tool.GetCommitNumberForPath(scope);
        RequireScopeMatchesCommits(count, scope, scopePath);

        var baseSemVersion = SemVersion.Parse(mainProject.BaseVersion);
        return new SemVersion(baseSemVersion.Major, baseSemVersion.Minor, count);
    }

    /// <summary>
    ///     Scope paths are interpreted from the repository root, matching how <c>Projects[].Path</c>
    ///     is already interpreted, so that a scope and an equivalent project entry cannot disagree
    ///     depending on where the caller happens to stand.
    /// </summary>
    private static string NormalizeScope(string scopePath)
    {
        var scope = scopePath.Trim().Replace('\\', '/').TrimEnd('/');

        if (scope.Length == 0)
            throw new ArgumentException("Scope cannot be empty.");

        if (Path.IsPathRooted(scope))
            throw new ArgumentException(
                $"Scope '{scopePath}' must be relative to the repository root, not an absolute path.");

        // The git command is assembled as a single string, so an embedded space would split into
        // two pathspecs and silently miscount. Refused rather than quietly producing a wrong number.
        if (scope.IndexOf(' ') >= 0)
            throw new ArgumentException($"Scope '{scopePath}' cannot contain spaces.");

        if (scope == ".." || scope.StartsWith("../", StringComparison.Ordinal) ||
            scope.Contains("/../", StringComparison.Ordinal))
            throw new ArgumentException(
                $"Scope '{scopePath}' must stay inside the repository; it cannot traverse upwards with '..'.");

        return scope;
    }

    /// <summary>
    ///     A scope matching nothing would otherwise return the base version unchanged — a
    ///     real-looking number carrying no information about the subtree. Failing here is what keeps
    ///     a mistyped scope from shipping as a plausible version.
    /// </summary>
    private void RequireScopeMatchesCommits(int count, string scope, string scopePath)
    {
        if (count > 0) return;

        var message = $"Scope '{scopePath}' matches no commits in this repository.";
        var suggestion = SuggestRootRelativeScope(scope);

        message += suggestion != null
            ? $" Did you mean '{suggestion}'? Scope is resolved from the repository root."
            : " Scope is resolved from the repository root, not the current directory.";

        throw new ArgumentException(message);
    }

    /// <summary>
    ///     Tab completion produces paths relative to the current directory, so the most likely
    ///     mistake is a scope that would have resolved from there. When that is the case, name the
    ///     root-relative form the caller wanted rather than only reporting the failure.
    /// </summary>
    private string SuggestRootRelativeScope(string scope)
    {
        try
        {
            var fromCurrentDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), scope));
            if (Directory.Exists(fromCurrentDirectory) == false && File.Exists(fromCurrentDirectory) == false)
                return null;

            var root = Path.GetFullPath(_root);
            if (fromCurrentDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return null;

            var relative = Path.GetRelativePath(root, fromCurrentDirectory).Replace('\\', '/');
            return relative == scope ? null : relative;
        }
        catch (Exception e)
        {
            _log.LogTrace($"Could not derive a scope suggestion; {e.Message}");
            return null;
        }
    }

    public AbcVersion Build(string projectNameFromConfig, AbcVersionOptions options)
    {
        var mainModel = GetMainModel();
        var semVersion = GetSemVersion(mainModel, projectNameFromConfig, options);
        var data = _tool.GetAllGitData();
        return new AbcVersion(semVersion, data, options.DateTime, _root, GetConfigPath(_root));
    }

    /// <summary>
    ///     Resolves the repository root by searching upwards from, in order: the configured path,
    ///     the current working directory, and the application root. Each candidate is searched
    ///     upwards rather than matched exactly, so running from a subdirectory of a repository
    ///     resolves correctly.
    /// </summary>
    /// <remarks>
    ///     The current-directory candidate matters wherever the application root cannot lead back to
    ///     the repository: a globally installed dotnet tool (whose app root is the install
    ///     directory) and any process in a container (whose app root is a fixed path holding no
    ///     .git). Previously only an exact match on the configured path was tried before falling
    ///     back to the application root, so both cases failed with "Cannot find repository root".
    /// </remarks>
    public static string GetRoot(SimpleEnvResult env, AbcVersionOptions options)
    {
        return FindGitDirectory(AsDirectory(options.PathToRepository))
               ?? FindGitDirectory(Directory.GetCurrentDirectory())
               ?? FindGitDirectory(env.AppRoot);


        // PathToRepository defaults to the process executable, which is a file, not a directory.
        static string AsDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return File.Exists(path) ? Path.GetDirectoryName(path) : path;
        }

        static string FindGitDirectory(string startPath)
        {
            if (string.IsNullOrEmpty(startPath)) return null;

            // Walk absolute paths. A relative candidate reaches "" after a couple of steps upwards,
            // and Path.Combine("", ".git") then tests the *current* directory — so a relative path
            // that is not in a repository at all would silently resolve to whichever repository the
            // process happens to be standing in, and the root reported back would be empty.
            string directory;
            try
            {
                directory = Path.GetFullPath(startPath);
            }
            catch (Exception)
            {
                return null;
            }

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