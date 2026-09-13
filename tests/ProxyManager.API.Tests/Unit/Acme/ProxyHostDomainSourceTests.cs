extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Acme;
using LettuceEncrypt;
using Microsoft.Extensions.DependencyInjection;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Acme;

[Trait("Category", "Unit")]
public class ProxyHostDomainSourceTests
{
    private static (ProxyHostDomainSource Source, FakeProxyHostRepository Repo) CreateSource()
    {
        var repo = new FakeProxyHostRepository();
        var services = new ServiceCollection();
        services.AddScoped<IProxyHostRepository>(_ => repo);
        var sp = services.BuildServiceProvider();
        var source = new ProxyHostDomainSource(sp.GetRequiredService<IServiceScopeFactory>());
        return (source, repo);
    }

    [Fact]
    public async Task GetDomains_ReturnsOnlyEnabledLetsEncryptModeHosts()
    {
        var (source, repo) = CreateSource();
        var leHost = ProxyHost.Create(["le.example.com"], DestinationUri.Parse("http://backend:8080"), tlsMode: TlsMode.LetsEncrypt);
        var manualHost = ProxyHost.Create(["manual.example.com"], DestinationUri.Parse("http://backend:8080"), tlsMode: TlsMode.Manual);
        var disabledLeHost = ProxyHost.Create(["disabled.example.com"], DestinationUri.Parse("http://backend:8080"), tlsMode: TlsMode.LetsEncrypt);
        disabledLeHost.Disable();
        repo.Seed(leHost, manualHost, disabledLeHost);

        var domains = (await source.GetDomains(CancellationToken.None)).ToList();

        var domainCert = Assert.Single(domains);
        Assert.Equal(["le.example.com"], domainCert.Domains);
    }

    [Fact]
    public async Task GetDomains_GroupsDomainsPerHost()
    {
        var (source, repo) = CreateSource();
        var host = ProxyHost.Create(
            ["primary.example.com", "alt.example.com"], DestinationUri.Parse("http://backend:8080"), tlsMode: TlsMode.LetsEncrypt);
        repo.Seed(host);

        var domains = (await source.GetDomains(CancellationToken.None)).ToList();

        var domainCert = Assert.Single(domains);
        Assert.Equal(new HashSet<string> { "primary.example.com", "alt.example.com" }, domainCert.Domains);
    }

    [Fact]
    public async Task GetDomains_WithNoLetsEncryptHosts_ReturnsEmpty()
    {
        var (source, repo) = CreateSource();
        repo.Seed(ProxyHost.Create(["manual.example.com"], DestinationUri.Parse("http://backend:8080")));

        var domains = await source.GetDomains(CancellationToken.None);

        Assert.Empty(domains);
    }
}
