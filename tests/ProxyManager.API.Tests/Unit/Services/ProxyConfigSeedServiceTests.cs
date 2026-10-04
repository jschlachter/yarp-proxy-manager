extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Services;
using ProxyManagerApp::West94.ProxyManager.Yarp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class ProxyConfigSeedServiceTests
{
    private static (ProxyConfigSeedService Service, DatabaseProxyConfigProvider Provider) CreateService(
        FakeProxyHostRepository repo, IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddScoped<IProxyHostRepository>(_ => repo);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var provider = new DatabaseProxyConfigProvider(scopeFactory);
        var service = new ProxyConfigSeedService(
            scopeFactory, config, provider, NullLogger<ProxyConfigSeedService>.Instance);
        return (service, provider);
    }

    private static IEnumerable<string> RoutedHosts(DatabaseProxyConfigProvider provider) =>
        provider.GetConfig().Routes.SelectMany(r => r.Match.Hosts ?? []);

    private static IConfiguration BuildConfig(params (string Key, string Value)[] entries)
    {
        var dict = entries.ToDictionary(e => e.Key, e => (string?)e.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task StartAsync_EmptyDb_WithHostBasedRoutes_SeedsHosts()
    {
        var repo = new FakeProxyHostRepository();
        var config = BuildConfig(
            ("ReverseProxy:Routes:myapp:ClusterId", "myapp"),
            ("ReverseProxy:Routes:myapp:Match:Hosts:0", "app.example.com"),
            ("ReverseProxy:Clusters:myapp:Destinations:primary:Address", "http://backend:8080")
        );

        var (service, provider) = CreateService(repo, config);

        await service.StartAsync(default);

        var seeded = await repo.GetAllAsync();
        Assert.Single(seeded);
        Assert.Contains("app.example.com", seeded[0].DomainNames);
        Assert.Contains("app.example.com", RoutedHosts(provider));
    }

    [Fact]
    public async Task StartAsync_NonEmptyDb_SkipsSeedingAndLoadsExistingRoutes()
    {
        var repo = new FakeProxyHostRepository();
        repo.Seed(ProxyHost.Create(["existing.example.com"], DestinationUri.Parse("http://old:8080")));
        var config = BuildConfig(
            ("ReverseProxy:Routes:myapp:ClusterId", "myapp"),
            ("ReverseProxy:Routes:myapp:Match:Hosts:0", "app.example.com"),
            ("ReverseProxy:Clusters:myapp:Destinations:primary:Address", "http://backend:8080")
        );

        var (service, provider) = CreateService(repo, config);

        await service.StartAsync(default);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(["existing.example.com"], RoutedHosts(provider));
    }

    [Fact]
    public async Task StartAsync_EmptyDb_NoReverseProxySection_LoadsEmptyConfig()
    {
        var repo = new FakeProxyHostRepository();
        var config = BuildConfig();

        var (service, provider) = CreateService(repo, config);

        await service.StartAsync(default);

        Assert.Empty(await repo.GetAllAsync());
        Assert.Empty(provider.GetConfig().Routes);
    }

    [Fact]
    public async Task StartAsync_OnlyPathBasedSystemRoutes_SkipsSeeding()
    {
        var repo = new FakeProxyHostRepository();
        var config = BuildConfig(
            ("ReverseProxy:Routes:apiRoute:ClusterId", "apiCluster"),
            ("ReverseProxy:Routes:apiRoute:Match:Path", "/api/{**catch-all}"),
            ("ReverseProxy:Routes:ui-route:ClusterId", "ui-cluster"),
            ("ReverseProxy:Routes:ui-route:Match:Path", "/{**catch-all}")
        );

        var (service, provider) = CreateService(repo, config);

        await service.StartAsync(default);

        Assert.Empty(await repo.GetAllAsync());
        Assert.Empty(provider.GetConfig().Routes);
    }

    [Fact]
    public async Task StartAsync_MultipleHostRoutes_SeedsAll()
    {
        var repo = new FakeProxyHostRepository();
        var config = BuildConfig(
            ("ReverseProxy:Routes:app1:ClusterId", "cluster1"),
            ("ReverseProxy:Routes:app1:Match:Hosts:0", "app1.example.com"),
            ("ReverseProxy:Clusters:cluster1:Destinations:primary:Address", "http://backend1:8080"),
            ("ReverseProxy:Routes:app2:ClusterId", "cluster2"),
            ("ReverseProxy:Routes:app2:Match:Hosts:0", "app2.example.com"),
            ("ReverseProxy:Clusters:cluster2:Destinations:primary:Address", "http://backend2:9090")
        );

        var (service, provider) = CreateService(repo, config);

        await service.StartAsync(default);

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(2, provider.GetConfig().Routes.Count);
    }
}
