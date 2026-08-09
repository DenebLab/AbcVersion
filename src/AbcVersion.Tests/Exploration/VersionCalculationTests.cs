using System;
using System.IO;
using Deneblab.AbcVersion;
using Deneblab.AbcVersionTests.Base;
using Xunit;

namespace Deneblab.AbcVersionTests.Exploration;

/// <summary>
///     Covers version calculation against this repository's own Git history and
///     <c>.abcversion.json</c>. These run wherever the repository is checked out, including inside
///     a container, which is what <see cref="ResolvesRepositoryRoot_WithoutExplicitPath" /> guards.
/// </summary>
[Collection(DLabAppCollection.NAME)]
public class VersionCalculationTests
{
    private readonly MainDLabAppFixture _appFixture;

    public VersionCalculationTests(MainDLabAppFixture appFixture)
    {
        _appFixture = appFixture;
    }

    /// <summary>
    ///     Regression test: repository discovery used to fail wherever the application root could
    ///     not lead back to the repository — inside a container (app root pinned to a fixed path
    ///     holding no .git) and for a globally installed tool (app root is the install directory).
    ///     This must resolve from the ambient location with no explicit path supplied.
    /// </summary>
    [Fact]
    public void ResolvesRepositoryRoot_WithoutExplicitPath()
    {
        var version = AbcVersionFactory.CreateAbcVersion();

        Assert.NotNull(version);
        Assert.False(string.IsNullOrWhiteSpace(version.SemVersion));
    }

    /// <summary>
    ///     Regression test: a path below the repository root used to be rejected, because only an
    ///     exact match on the configured path was tried. Callers run from subdirectories routinely,
    ///     so the search must walk upwards.
    /// </summary>
    [Fact]
    public void ResolvesRepositoryRoot_FromASubdirectory()
    {
        var deepPath = Path.Combine(AppContext.BaseDirectory, "no", "such", "nested", "dir");

        var version = AbcVersionFactory
            .CreateBuilder()
            .SetRepositoryRoot(deepPath)
            .UseLogger(_appFixture.Log)
            .Build();

        Assert.False(string.IsNullOrWhiteSpace(version.SemVersion));
    }

    [Fact]
    public void ProducesVersionConsistentWithItsOwnParts()
    {
        var version = AbcVersionFactory
            .CreateBuilder()
            .UseLogger(_appFixture.Log)
            .Build();

        Assert.Equal($"{version.Major}.{version.Minor}.{version.Patch}", version.SemVersion);
        Assert.Matches(@"^\d+\.\d+\.\d+$", version.SemVersion);
    }

    [Fact]
    public void ReadsGitStateFromTheRepository()
    {
        var version = AbcVersionFactory
            .CreateBuilder()
            .UseLogger(_appFixture.Log)
            .Build();

        Assert.False(string.IsNullOrWhiteSpace(version.GitBranch));
        Assert.Matches("^[0-9a-f]{40}$", version.GitSha);
        Assert.Contains($"Branch.{version.GitBranch}.", version.InformationalVersion);
    }

    [Fact]
    public void UnknownProjectName_ThrowsWithTheProjectNamed()
    {
        var builder = AbcVersionFactory
            .CreateBuilder()
            .UseLoggerFactory(_appFixture.LogFLogger);

        var ex = Assert.Throws<ArgumentException>(() => builder.Build("no-such-project"));

        Assert.Contains("no-such-project", ex.Message);
    }
}
