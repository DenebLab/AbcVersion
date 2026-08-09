using System;
using System.Diagnostics;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Deneblab.AbcVersionTests.Base;

/// <summary>
///     A throwaway Git repository with a known commit shape.
/// </summary>
/// <remarks>
///     The existing tests run against this repository's own history, which is right for them but
///     cannot express "a subtree with exactly three commits" or "no configuration file at all".
///     Scope calculation needs both, and needs them stable as commits land here.
/// </remarks>
public sealed class TempGitRepository : IDisposable
{
    private TempGitRepository(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string ConfigPath => Path.Combine(Root, ".abcversion.json");

    public void Dispose()
    {
        if (Directory.Exists(Root) == false) return;

        try
        {
            // Git marks objects read-only, which blocks a plain recursive delete on Windows.
            foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
            // A leaked temp directory must never fail a test run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static TempGitRepository Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "abcversion-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var repository = new TempGitRepository(root);
        repository.Git("init -q .");
        repository.Git("config user.email test@example.com");
        repository.Git("config user.name test");
        repository.Git("config commit.gpgsign false");
        return repository;
    }

    public TempGitRepository WriteConfig(string json)
    {
        File.WriteAllText(ConfigPath, json);
        return this;
    }

    /// <summary>Creates or touches a file and commits it, so the path gains exactly one commit.</summary>
    public TempGitRepository CommitFile(string relativePath, string content = null)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? Root);
        File.WriteAllText(full, content ?? Guid.NewGuid().ToString("N"));

        Git("add -A");
        Git($"commit -q -m \"touch {relativePath}\"");
        return this;
    }

    /// <summary>First-parent commit count for a path — the number the tool is expected to produce.</summary>
    public int FirstParentCommitCount(string relativePath = null)
    {
        var command = "rev-list HEAD --count --first-parent";
        if (string.IsNullOrEmpty(relativePath) == false) command += $" -- {relativePath}";
        return int.Parse(Git(command).Trim());
    }

    private string Git(string arguments)
    {
        var info = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(info);
        if (process == null) throw new InvalidOperationException("Could not start git.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments} failed ({process.ExitCode}): {error}");

        return output;
    }
}
