using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Deneblab.AbcVersionTests.Base;

public class HostFixture : IDisposable
{
    public HostFixture(Action<IServiceCollection> configureServices = null)
    {
        var builder = Host.CreateApplicationBuilder();
        configureServices?.Invoke(builder.Services); // Custom configuration
        AppHost = builder.Build();
    }

    public IHost AppHost { get; }

    public void Dispose()
    {
        AppHost?.Dispose();
    }
}

public class AbcVersionHostFixture : HostFixture
{
    public AbcVersionHostFixture() :
        base(services => { })
    {
    }
}

[CollectionDefinition(NAME)]
public sealed class AbcVersionHostCollection : ICollectionFixture<AbcVersionHostFixture>
{
    public const string NAME = nameof(AbcVersionHostCollection);
}

/// <summary>
///     Example of a custom service fixture
/// </summary>
public class CustomServiceFixture : HostFixture
{
    public CustomServiceFixture() : base(services =>
    {
        services.AddSingleton<CustomService>(); // Custom service
    })
    {
    }
}

public class CustomService;