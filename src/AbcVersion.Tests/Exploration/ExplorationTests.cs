using Deneblab.AbcVersion;
using Deneblab.AbcVersionTests.Base;
using Xunit;

namespace Deneblab.AbcVersionTests.Exploration;

[Collection(DLabAppCollection.NAME)]
public class ExplorationTests
{
    private readonly MainDLabAppFixture _appFixture;

    public ExplorationTests(MainDLabAppFixture appFixture)
    {
        _appFixture = appFixture;
    }

    [Fact]
    public void FactMethodName()
    {

        var ver1 = AbcVersionFactory.CreateAbcVersion();
        var ver = AbcVersionFactory
            .CreateBuilder()
            .UseLogger(_appFixture.Log)
            .Build();
    }

    [Fact]
    public void FactMethodName2()
    {

       // var ver1 = AbcVersionFactory.CreateAbcVersion("Deneblab.Common");
   
        var ver = AbcVersionFactory
            .CreateBuilder()
            .SetRepositoryRoot("W:\\DenebLab\\DeneblabLibraries")
            .UseLoggerFactory(_appFixture.LogFLogger)
            .Build("Deneblab.Common");
    }
}