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
    private sealed class SpyReloader : IProxyConfigReloader
    {
        public int ReloadCount { get; private set; }
        public void Reload() => ReloadCount++;
    }

    private static IServiceScopeFactory BuildScopeFactory(FakeProxyHostRepository repo)
    {
        var services = new ServiceCollection();
        services.AddScoped<IProxyHostRepository>(_ => repo);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static IConfiguration BuildConfig(params (string Key, string Value)[] entries)
    {
        var dict = entries.ToDictionary(e => e.Key, e => (string?)e.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task StartAsync_EmptyDb_WithHostBasedRoutes_SeedsHosts()
    {
        var repo = new FakeProxyHostRepository();
        var reloader = new SpyReloader();
        var config = BuildConfig(
            ("ReverseProxy:Routes:myapp:ClusterId", "myapp"),
            ("ReverseProxy:Routes:myapp:Match:Hosts:0", "app.example.com"),
            ("ReverseProxy:Clusters:myapp:Destinations:primary:Address", "http://backend:8080")
        );

        var service = new ProxyConfigSeedService(
            BuildScopeFactory(repo), config, reloader,
            NullLogger<ProxyConfigSeedService>.Instance);

        await service.StartAsync(default);

        var seeded = await repo.GetAllAsync();
        Assert.Single(seeded);
        Assert.Contains("app.example.com", seeded[0].DomainNames);
        Assert.Equal(1, reloader.ReloadCount);
    }

    [Fact]
    public async Task StartAsync_NonEmptyDb_SkipsSeedingAndReload()
    {
        var repo = new FakeProxyHostRepository();
        repo.Seed(ProxyHost.Create(["existing.example.com"], DestinationUri.Parse("http://old:8080")));
        var reloader = new SpyReloader();
        var config = BuildConfig(
            ("ReverseProxy:Routes:myapp:ClusterId", "myapp"),
            ("ReverseProxy:Routes:myapp:Match:Hosts:0", "app.example.com"),
            ("ReverseProxy:Clusters:myapp:Destinations:primary:Address", "http://backend:8080")
        );

        var service = new ProxyConfigSeedService(
            BuildScopeFactory(repo), config, reloader,
            NullLogger<ProxyConfigSeedService>.Instance);

        await service.StartAsync(default);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(0, reloader.ReloadCount);
    }

    [Fact]
    public async Task StartAsync_NoReverseProxySection_SkipsSeedingAndReload()
    {
        var repo = new FakeProxyHostRepository();
        var reloader = new SpyReloader();
        var config = BuildConfig();

        var service = new ProxyConfigSeedService(
            BuildScopeFactory(repo), config, reloader,
            NullLogger<ProxyConfigSeedService>.Instance);

        await service.StartAsync(default);

        Assert.Empty(await repo.GetAllAsync());
        Assert.Equal(0, reloader.ReloadCount);
    }

    [Fact]
    public async Task StartAsync_OnlyPathBasedSystemRoutes_SkipsSeedingAndReload()
    {
        var repo = new FakeProxyHostRepository();
        var reloader = new SpyReloader();
        var config = BuildConfig(
            ("ReverseProxy:Routes:apiRoute:ClusterId", "apiCluster"),
            ("ReverseProxy:Routes:apiRoute:Match:Path", "/api/{**catch-all}"),
            ("ReverseProxy:Routes:ui-route:ClusterId", "ui-cluster"),
            ("ReverseProxy:Routes:ui-route:Match:Path", "/{**catch-all}")
        );

        var service = new ProxyConfigSeedService(
            BuildScopeFactory(repo), config, reloader,
            NullLogger<ProxyConfigSeedService>.Instance);

        await service.StartAsync(default);

        Assert.Empty(await repo.GetAllAsync());
        Assert.Equal(0, reloader.ReloadCount);
    }

    [Fact]
    public async Task StartAsync_MultipleHostRoutes_SeedsAll()
    {
        var repo = new FakeProxyHostRepository();
        var reloader = new SpyReloader();
        var config = BuildConfig(
            ("ReverseProxy:Routes:app1:ClusterId", "cluster1"),
            ("ReverseProxy:Routes:app1:Match:Hosts:0", "app1.example.com"),
            ("ReverseProxy:Clusters:cluster1:Destinations:primary:Address", "http://backend1:8080"),
            ("ReverseProxy:Routes:app2:ClusterId", "cluster2"),
            ("ReverseProxy:Routes:app2:Match:Hosts:0", "app2.example.com"),
            ("ReverseProxy:Clusters:cluster2:Destinations:primary:Address", "http://backend2:9090")
        );

        var service = new ProxyConfigSeedService(
            BuildScopeFactory(repo), config, reloader,
            NullLogger<ProxyConfigSeedService>.Instance);

        await service.StartAsync(default);

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(1, reloader.ReloadCount);
    }
}
