using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;


// ReSharper disable once CheckNamespace

namespace Deneblab.AbcVersion;

public class AbcVersion
{
    private readonly ILogger _log;

    private readonly Dictionary<string, string> _store = [];

    public AbcVersion()
    {
    }


    public AbcVersion(
        SemVersion sem,
        Internal.AbcVersionGitData data,
        DateTime dateTime,
        string repositoryRoot = null,
        string configPath = null)
    {
        Major = sem.Major;
        Minor = sem.Minor;
        Patch = sem.Patch;
        GitSha = data.GitSha;
        GitBranch = data.GitBranch;
        Machine = Environment.MachineName;
        DateTime = dateTime;
        Meta = sem.Meta;
        PreRelease = sem.PreRelease;
        VersionString = sem.VersionString;
        RepositoryRoot = repositoryRoot;
        ConfigPath = configPath;
    }
    public AbcVersion(ILogger log)
    {
        _log = log;
    }



    public string VersionString { get; }

    public string PreRelease { get; }

    public string Meta { get; }

    public string GitBranch { get; }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string Machine { get; } = null!;

    public string GitSha { get; }
    public DateTime DateTime { get; }

    public string SemVersion => $"{Major}.{Minor}.{Patch}";


    public string ShortBuildMetaData => $"Branch.{GitBranch}." +
                                        $"DateTime.{DateTime:s}Z." +
                                        $"Machine.{Machine}." +
                                        $"Sha.{GitSha}.";

    public string InformationalVersion => $"{SemVersion}+{ShortBuildMetaData}";

    /// <summary>
    ///     The repository root actually resolved for this calculation, after searching upwards from
    ///     the configured path. Callers pass a path that may sit below the root, so this is the only
    ///     reliable answer to "which repository produced this version".
    /// </summary>
    /// <remarks>
    ///     Deliberately excluded from serialization: this is an absolute machine path, and the
    ///     command line tool emits the serialized form as its default output. Publishing it would
    ///     put build agent filesystem paths into CI logs and anything embedding that JSON.
    /// </remarks>
    [JsonIgnore]
    public string RepositoryRoot { get; }

    /// <summary>
    ///     Full path to the <c>.abcversion.json</c> that was used, or <c>null</c> when none exists
    ///     and the defaults applied. Resolved against <see cref="RepositoryRoot" />, not against the
    ///     path the caller supplied.
    /// </summary>
    /// <remarks>Excluded from serialization for the same reason as <see cref="RepositoryRoot" />.</remarks>
    [JsonIgnore]
    public string ConfigPath { get; }


    public class AbcVersionGitData(
        string gitSha,
        int gitCommitsAll,
        string gitBranch,
        int gitCommitsCurrentBranch,
        int gitCommitsCurrentBranchFirstParent)
    {
        public string GitBranch { get; } = gitBranch;
        public int GitCommitsCurrentBranch { get; } = gitCommitsCurrentBranch;
        public int GitCommitsCurrentBranchFirstParent { get; } = gitCommitsCurrentBranchFirstParent;
        public string GitSha { get; } = gitSha;
        public int GitCommitsAll { get; } = gitCommitsAll;
    }
}