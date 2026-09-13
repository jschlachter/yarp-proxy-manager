using LettuceEncrypt;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.Acme;

/// <summary>
/// Feeds LettuceEncrypt the domain sets it should manage certificates for: one <see cref="MultipleDomainCert"/>
/// per enabled <see cref="ProxyHost"/> opted into <see cref="TlsMode.LetsEncrypt"/>. Resolves its own DI scope
/// per call since it is registered as a singleton (matching LettuceEncrypt's own registration expectations)
/// while <see cref="IProxyHostRepository"/> is scoped.
/// </summary>
public sealed class ProxyHostDomainSource(IServiceScopeFactory scopeFactory) : IDomainSource
{
    public async Task<IEnumerable<IDomainCert>> GetDomains(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var proxyHosts = scope.ServiceProvider.GetRequiredService<IProxyHostRepository>();

        var hosts = await proxyHosts.GetAllAsync(cancellationToken);

        return hosts
            .Where(h => h.TlsMode == TlsMode.LetsEncrypt && h.IsEnabled)
            .Select(h => (IDomainCert)new MultipleDomainCert { OrderedDomains = [.. h.DomainNames] })
            .ToList();
    }
}
