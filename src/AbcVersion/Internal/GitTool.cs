using System;
using Microsoft.Extensions.Logging;

namespace Deneblab.AbcVersion.Internal;

internal class GitTool
{
    private const string _TOOL_NAME = "git";
    private readonly CliRunner _cliRunner;
    private readonly ILogger _log;
    private readonly string _repositoryRoot;

    public GitTool(ILogger log, string repositoryRoot)
    {
        _log = log;
        _repositoryRoot = repositoryRoot;
        _cliRunner = new CliRunner(_log);
    }

    public string GetVersion()
    {
        return RunOneLiner("git --version");
    }

    public string GetBranch()
    {
        return RunOneLiner("git rev-parse --abbrev-ref HEAD");
    }

    public string GetRepositoryRoot()
    {
        return RunOneLiner("git rev-parse --show-toplevel");
    }

    public string GetHash()
    {
        return RunOneLiner("git log --max-count=1 --pretty=format:%H HEAD");
    }

    public int GetCommitNumberAll()
    {
        return RunOneLinerInt("git rev-list --all --count");
    }

    public int GetCommitNumberCurrentBranch()
    {
        return RunOneLinerInt("git rev-list HEAD --count");
    }

    public int GetCommitNumberCurrentBranchFirstParent()
    {
        return RunOneLinerInt("git rev-list HEAD --count --first-parent");
    }

    public int GetCommitNumberCurrentBranchFirstParent(string sinceSha)
    {
        return RunOneLinerInt($"git rev-list {sinceSha}..HEAD --count --first-parent");
    }

    public bool GetShaExists(string currentBranch, string sha)
    {
        var r1 = RunOneLiner($"git branch {currentBranch} --contains {sha}");
        return r1.Contains("error:") == false;
    }

    private string RunOneLiner(string command)
    {
        var (tool, arguments) = SplitCommand(command);
        var text = _cliRunner.RunReturnText(_TOOL_NAME, arguments, _repositoryRoot);
        return text;
    }

    private int RunOneLinerInt(string command)
    {
        var (tool, arguments) = SplitCommand(command);
        var text = _cliRunner.RunReturnText(tool, arguments, _repositoryRoot);
        var number = int.Parse(text);
        return number;
    }

    private (string tool, string arguments) SplitCommand(string command)
    {
        var cmd = command.Trim();
        _log.LogTrace($"GitCmd; {command}");
        var pos1 = cmd.IndexOf(" ", StringComparison.Ordinal);
        var tool = pos1 > 0 ? cmd.Substring(0, pos1) : cmd;
        var arguments = pos1 > 0 ? cmd.Substring(pos1) : "";
        return (tool, arguments);
    }

    internal AbcVersionGitData GetAllGitData()
    {
        var hash = GetHash();
        var branch = GetBranch();
        var ret = new AbcVersionGitData(hash, branch);
        return ret;
    }

    public int GetCommitNumberForPath(string path, string sinceSha = null)
    {
        path = string.IsNullOrEmpty(path) ? "." : path;

        var command = string.IsNullOrEmpty(sinceSha)
            ? "git rev-list HEAD --count --first-parent"
            : $"git rev-list {sinceSha}..HEAD --count --first-parent";

        if (path != ".") command = $"{command} -- {path}";

        return RunOneLinerInt(command);
    }
}