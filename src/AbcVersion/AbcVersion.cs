using System;
using System.Collections.Generic;
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


    public AbcVersion(SemVersion sem, Internal.AbcVersionGitData data, DateTime dateTime)
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


    public string AssemblyVersion => $"{Major}.0.0.0";
    public string FileVersion => $"{Major}.{Minor}.{Patch}.0";

    public string ShortBuildMetaData => $"Branch.{GitBranch}." +
                                        $"DateTime.{DateTime:s}Z." +
                                        $"Machine.{Machine}." +
                                        $"Sha.{GitSha}.";

    public string InformationalVersion => $"{SemVersion}+{ShortBuildMetaData}";


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