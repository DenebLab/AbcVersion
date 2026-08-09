using System;
using System.IO;
using Deneblab.AbcVersion.Internal;
using Deneblab.StashLock.Cli.Common.Simple;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deneblab.AbcVersion;

public class AbcVersionOptions
{
    public DateTime DateTime { get; set; } = DateTime.UtcNow;
    public string PathToRepository { get; set; } = Environment.ProcessPath;

    /// <summary>
    ///     Optional subtree, relative to the repository root, that the commit count is narrowed to.
    ///     Unlike <see cref="PathToRepository" /> — which only says which repository to read — this
    ///     scopes the version itself, without needing a <c>Projects</c> entry in the configuration.
    ///     Mutually exclusive with a named project.
    /// </summary>
    public string ScopePath { get; set; }
}

public static class AbcVersionFactory
{
    private static readonly Holder<AbcVersion> _instance = new();

    public static AbcVersionBuilder CreateBuilder()
    {
        return new AbcVersionBuilder(_instance);
    }

    public static AbcVersionBuilder CreateOneBuilder()
    {
        _instance.MakeOnlyOne = true;
        return new AbcVersionBuilder(_instance);
    }

    public static AbcVersion CreateAbcVersion(string projectNameFromConfig = AbcVersionConsts.MAIN_PROJECT_MARKER)
    {
        return new AbcVersionBuilder(_instance).Build(projectNameFromConfig);
    }

    /// <summary>
    ///     Resolves the repository root the same way a version calculation would, searching upwards
    ///     from <paramref name="path" />. Returns <c>null</c> when no repository is found.
    /// </summary>
    /// <remarks>
    ///     Exists so that callers reporting on a calculation — diagnostics, error messages listing
    ///     configured projects — can read the configuration from the same place the calculation did,
    ///     instead of re-implementing the search and drifting away from it.
    /// </remarks>
    public static string ResolveRepositoryRoot(string path)
    {
        var options = new AbcVersionOptions { PathToRepository = path };
        return AbcVersionCreator.GetRoot(AbcVersionBuilder._env, options);
    }

    internal class Holder<T> where T : class
    {
        public bool MakeOnlyOne { get; set; }
        public T Instance { private set; get; }
        public bool IsSet => Instance != null;

        public void SetValueIfNeeded(T obj)
        {
            if (MakeOnlyOne) Instance = obj;
        }
    }


    public class AbcVersionBuilder
    {
        internal static readonly SimpleEnvResult _env = SimpleEnv.Detect("Deneblab", "AbcVersion");

        private readonly Holder<AbcVersion> _holder;
        private readonly AbcVersionOptions _options;

        private ILogger _log = NullLogger.Instance;


        internal AbcVersionBuilder(Holder<AbcVersion> holder)
        {
            _holder = holder;
            _options = new AbcVersionOptions();
        }

        public AbcVersionBuilder SetDateTime(DateTime dateTime)
        {
            if (_holder.IsSet) return this;
            _options.DateTime = dateTime;
            return this;
        }

        public AbcVersionBuilder SetRepositoryRoot(string path)
        {
            if (_holder.IsSet) return this;
            _options.PathToRepository = path;
            return this;
        }

        /// <summary>
        ///     Narrows the commit count to a subtree of the repository, resolved from the repository
        ///     root. Cannot be combined with a named project.
        /// </summary>
        public AbcVersionBuilder SetScope(string scopePath)
        {
            if (_holder.IsSet) return this;
            _options.ScopePath = scopePath;
            return this;
        }

        public AbcVersionBuilder UseOptions(Action<AbcVersionOptions> action)
        {
            if (_holder.IsSet) return this;
            action(_options);
            return this;
        }


        // Method to set the logger
        public AbcVersionBuilder UseLogger(ILogger logger)
        {
            if (_holder.IsSet) return this;
            _log = logger;
            _log.LogTrace("Logger configured; ");
            return this;
        }


        // Or accept an ILoggerFactory for more flexibility
        public AbcVersionBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
        {
            return UseLogger(loggerFactory.CreateLogger("AbcVersion"));
        }


        // Method to build the library (returns the configured static class or instance)
        public AbcVersion Build(string projectNameFromConfig = AbcVersionConsts.MAIN_PROJECT_MARKER)
        {
            if (_holder.IsSet) return _holder.Instance;
            var root = AbcVersionCreator.GetRoot(_env, _options);
            if (root == null) throw new DirectoryNotFoundException("Cannot find repository root (.git directory)");
            _log.LogDebug($"RepositoryRoot; {root}");
            var tool = new GitTool(_log, root);
            var creator = new AbcVersionCreator(_log, root, tool);
            var abcVersion = creator.Build(projectNameFromConfig, _options);
            _holder.SetValueIfNeeded(abcVersion);
            _log.LogDebug($"SemVersion {abcVersion.SemVersion} for project {projectNameFromConfig}");
            return abcVersion;
        }
    }
}