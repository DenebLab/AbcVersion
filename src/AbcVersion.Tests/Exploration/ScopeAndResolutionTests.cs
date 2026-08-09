using System;
using System.IO;
using Deneblab.AbcVersion;
using Deneblab.AbcVersionTests.Base;
using Xunit;

namespace Deneblab.AbcVersionTests.Exploration;

/// <summary>
///     Covers <c>ScopePath</c> — versioning a subtree with no <c>Projects</c> entry — and the
///     repository/configuration resolution that diagnostics report.
/// </summary>
/// <remarks>
///     These run against a throwaway repository rather than this one, because the interesting cases
///     are exact commit counts per subtree and the absence of a configuration file.
/// </remarks>
[Collection(DLabAppCollection.NAME)]
public class ScopeAndResolutionTests
{
    private const string _CONFIG_WITH_PROJECT = """
        {
          "BaseVersion": "0.2.0",
          "Projects": {
            "alpha": { "Name": "alpha", "Path": "src/alpha", "BaseVersion": "0.2.0" }
          }
        }
        """;

    private readonly MainDLabAppFixture _appFixture;

    public ScopeAndResolutionTests(MainDLabAppFixture appFixture)
    {
        _appFixture = appFixture;
    }

    /// <summary>Three commits under src/alpha, two elsewhere, so scoping is observable.</summary>
    private static TempGitRepository CreateRepository(string config = _CONFIG_WITH_PROJECT)
    {
        var repository = TempGitRepository.Create();

        if (config != null) repository.WriteConfig(config);

        repository.CommitFile("readme.md");
        repository.CommitFile("src/alpha/one.txt");
        repository.CommitFile("src/alpha/two.txt");
        repository.CommitFile("src/alpha/three.txt");
        repository.CommitFile("src/beta/one.txt");
        return repository;
    }

    private AbcVersion.AbcVersion Build(TempGitRepository repository, string scope = null, string project = ".")
    {
        var builder = AbcVersionFactory
            .CreateBuilder()
            .SetRepositoryRoot(repository.Root)
            .UseLogger(_appFixture.Log);

        if (scope != null) builder = builder.SetScope(scope);

        return builder.Build(project);
    }

    /// <summary>
    ///     The point of the flag: a subtree gets its own patch number without any configuration
    ///     entry. Asserted against git rather than a literal, so the test survives new commits.
    /// </summary>
    [Fact]
    public void Scope_CountsOnlyCommitsTouchingTheSubtree()
    {
        using var repository = CreateRepository();

        var scoped = Build(repository, "src/alpha");
        var wholeRepository = Build(repository);

        Assert.Equal(repository.FirstParentCommitCount("src/alpha"), scoped.Patch);
        Assert.Equal(repository.FirstParentCommitCount(), wholeRepository.Patch);
        Assert.True(scoped.Patch < wholeRepository.Patch, "scoping must narrow the count");
    }

    /// <summary>
    ///     A scope and a project entry naming the same directory at the same base version must not
    ///     disagree — that equivalence is what makes the entry optional rather than merely tedious.
    /// </summary>
    [Fact]
    public void Scope_AgreesWithAnEquivalentProjectEntry()
    {
        using var repository = CreateRepository();

        var scoped = Build(repository, "src/alpha");
        var configured = Build(repository, project: "alpha");

        Assert.Equal(configured.SemVersion, scoped.SemVersion);
    }

    /// <summary>
    ///     Without this, a mistyped scope returns the base version unchanged: a real-looking number
    ///     that describes nothing. This is the guard that keeps the flag honest.
    /// </summary>
    [Fact]
    public void Scope_MatchingNoCommits_Throws()
    {
        using var repository = CreateRepository();

        var ex = Assert.Throws<ArgumentException>(() => Build(repository, "src/nope"));

        Assert.Contains("src/nope", ex.Message);
        Assert.Contains("no commits", ex.Message);
    }

    /// <summary>
    ///     Tab completion produces cwd-relative paths, so the likeliest mistake gets the answer
    ///     rather than only the rejection.
    /// </summary>
    [Fact]
    public void Scope_ResolvingOnlyFromTheCurrentDirectory_SuggestsTheRootRelativeForm()
    {
        using var repository = CreateRepository();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(Path.Combine(repository.Root, "src"));

            var ex = Assert.Throws<ArgumentException>(() => Build(repository, "alpha"));

            Assert.Contains("src/alpha", ex.Message);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    /// <summary>
    ///     Both narrow the commit count, so a precedence rule would leave one silently ignored.
    ///     Enforced in the library, not only at the command line.
    /// </summary>
    [Fact]
    public void Scope_CombinedWithProject_Throws()
    {
        using var repository = CreateRepository();

        var ex = Assert.Throws<ArgumentException>(() => Build(repository, "src/alpha", "alpha"));

        Assert.Contains("cannot be combined", ex.Message);
    }

    [Theory]
    [InlineData("/etc", "absolute")]
    [InlineData("../outside", "upwards")]
    [InlineData("src/with space", "spaces")]
    public void Scope_RejectsPathsItCannotCountCorrectly(string scope, string _)
    {
        using var repository = CreateRepository();

        Assert.Throws<ArgumentException>(() => Build(repository, scope));
    }

    /// <summary>
    ///     A snapshot pins a specific commit deliberately, and must keep winning over a scope.
    /// </summary>
    [Fact]
    public void Snapshot_OverridesScope()
    {
        using var repository = TempGitRepository.Create();
        repository.WriteConfig("""{ "BaseVersion": "0.2.0" }""");
        repository.CommitFile("src/alpha/one.txt");

        var sha = Build(repository).GitSha;
        repository.WriteConfig($$"""
            { "BaseVersion": "0.2.0", "Snapshots": { "{{sha}}": "9.9.9" } }
            """);

        Assert.Equal("9.9.9", Build(repository, "src/alpha").SemVersion);
    }

    /// <summary>
    ///     The reported bug: a path below the root resolved the configuration correctly but reported
    ///     it as missing, because the report was built from the argument instead of the resolution.
    /// </summary>
    [Fact]
    public void ResolvedRoot_AndConfigPath_ComeFromTheResolution_NotTheArgument()
    {
        using var repository = CreateRepository();
        var subdirectory = Path.Combine(repository.Root, "src", "alpha");

        var version = AbcVersionFactory
            .CreateBuilder()
            .SetRepositoryRoot(subdirectory)
            .UseLogger(_appFixture.Log)
            .Build();

        Assert.Equal(Path.GetFullPath(repository.Root), Path.GetFullPath(version.RepositoryRoot));
        Assert.NotNull(version.ConfigPath);
        Assert.True(File.Exists(version.ConfigPath));
        Assert.Equal(Path.GetFullPath(repository.ConfigPath), Path.GetFullPath(version.ConfigPath));
    }

    /// <summary>
    ///     "No configuration" has to be distinguishable from "configuration reported wrongly", which
    ///     is the confusion the fix removes.
    /// </summary>
    [Fact]
    public void ConfigPath_IsNull_WhenTheRepositoryHasNoConfiguration()
    {
        using var repository = CreateRepository(null);

        var version = Build(repository);

        Assert.Null(version.ConfigPath);
        Assert.Equal(0, version.Major);
        Assert.Equal(0, version.Minor);
    }

    /// <summary>
    ///     The resolved root must be absolute even when the caller passes a relative path, otherwise
    ///     the walk upwards can land on an empty string and silently test the current directory.
    /// </summary>
    [Fact]
    public void ResolvedRoot_IsAbsolute_ForARelativePath()
    {
        using var repository = CreateRepository();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(repository.Root);

            var version = AbcVersionFactory
                .CreateBuilder()
                .SetRepositoryRoot("src")
                .UseLogger(_appFixture.Log)
                .Build();

            Assert.True(Path.IsPathRooted(version.RepositoryRoot));
            Assert.Equal(Path.GetFullPath(repository.Root), Path.GetFullPath(version.RepositoryRoot));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }
}
