using System;
using Deneblab.Common.Host;
using Deneblab.Common.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Deneblab.AbcVersionTests.Base;

public class DLabAppFixture
{
    public DLabAppFixture(Action<LoggingOptions> action = null)
    {
        var builder = DLabHost.CreateDLabAppBuilder();
        builder.CreateAppEnv();
        builder.AddAssembly(typeof(DLabAppFixture).Assembly);
        builder.CreateDefaultLogger();

        App = builder.Build();
        Env = App.GetAppEnv();
        Log = App.GetCurrentClassLogger();
        LogFLogger = App.GetLoggerFactory();
    }

    public DLabApp App { get; }
    public AppEnv Env { get; }
    public ILogger Log { get; }
    public ILoggerFactory LogFLogger { get; }
}

public class MainDLabAppFixture : DLabAppFixture
{
    public MainDLabAppFixture() :
        base(services => { })
    {
    }
}

[CollectionDefinition(NAME)]
public sealed class DLabAppCollection : ICollectionFixture<MainDLabAppFixture>
{
    public const string NAME = nameof(MainDLabAppFixture);
}