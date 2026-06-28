extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Yarp;
using Microsoft.Extensions.DependencyInjection;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Yarp;

[Trait("Category", "Unit")]
public class DatabaseProxyConfigProviderTests
{
    private static (DatabaseProxyConfigProvider Provider, FakeProxyHostRepository Repo) CreateProvider()
    {
        var repo = new FakeProxyHostRepository();
        var services = new ServiceCollection();
        services.AddScoped<IProxyHostRepository>(_ => repo);
        var sp = services.BuildServiceProvider();
        var provider = new DatabaseProxyConfigProvider(sp.GetRequiredService<IServiceScopeFactory>());
        return (provider, repo);
    }

    [Fact]
    public void GetConfig_BeforeAnyReload_ReturnsEmptyConfig()
    {
        var (provider, _) = CreateProvider();

        var config = provider.GetConfig();

        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public void GetConfig_AfterReload_ReturnsTranslatedRoutesFromRepository()
    {
        var (provider, repo) = CreateProvider();
        repo.Seed(ProxyHost.Create(["app.example.com"], DestinationUri.Parse("http://backend:8080")));

        provider.Reload();
        var config = provider.GetConfig();

        Assert.Single(config.Routes);
        Assert.Single(config.Clusters);
        var matchHosts = config.Routes[0].Match.Hosts;
        Assert.NotNull(matchHosts);
        Assert.Contains("app.example.com", matchHosts);
    }

    [Fact]
    public void Reload_FiresChangeTokenOnPreviousConfig()
    {
        var (provider, _) = CreateProvider();
        var config = provider.GetConfig();
        var changed = false;
        config.ChangeToken.RegisterChangeCallback(_ => changed = true, null);

        provider.Reload();

        Assert.True(changed);
    }

    [Fact]
    public void GetConfig_AfterTwoReloads_ReturnsLatestData()
    {
        var (provider, repo) = CreateProvider();
        repo.Seed(ProxyHost.Create(["first.example.com"], DestinationUri.Parse("http://first:8080")));
        provider.Reload();

        repo.Seed(ProxyHost.Create(["second.example.com"], DestinationUri.Parse("http://second:8080")));
        provider.Reload();

        var config = provider.GetConfig();
        Assert.Equal(2, config.Routes.Count);
    }

    [Fact]
    public void GetConfig_NewConfigAfterReload_HasFreshChangeToken()
    {
        var (provider, _) = CreateProvider();
        provider.Reload();
        var config = provider.GetConfig();

        Assert.False(config.ChangeToken.HasChanged);
    }
}
