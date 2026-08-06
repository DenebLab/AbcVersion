using System.Collections.Generic;

namespace Deneblab.AbcVersion.Internal;

public class AbcVersionGitData(string gitSha, string gitBranch)
{
    public string GitBranch { get; } = gitBranch;

    public string GitSha { get; } = gitSha;
}

internal class AbcVersionConfig
{
    public string BaseVersion { get; set; } = "0.0.0";
    public Dictionary<string, RootBranch> Branches { get; set; } = new();
    public Dictionary<string, Project> Projects { get; set; } = new();
    public Dictionary<string, string> Snapshots { get; set; } = new();
}

internal class RootBranch
{
    public string Version { get; set; }
    public string ParentSha { get; set; }
}

internal class Branch
{
    public string Name { get; set; }
    public string BaseVersion { get; set; } = "0.0.0";
    public StartPoint StartPoint { get; set; }
}

internal class StartPoint
{
    public string Version { get; set; }
    public string ParentSha { get; set; }
}

internal class Project
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string BaseVersion { get; set; } = "0.0.0";
    public Dictionary<string, Branch> Branches { get; set; } = new();
}

internal class AbcVersionMainModel
{
    public Dictionary<string, Project> Projects { get; set; } = new();
    public Dictionary<string, string> Snapshots { get; set; } = new();
}

