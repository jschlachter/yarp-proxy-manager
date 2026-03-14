using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public class ProxyHost : Entity
{
    private List<string> _domainNames;

    private ProxyHost(Guid id, List<string> domainNames, DestinationUri destination, bool isEnabled, ProxyCertificate? certificate)
    {
        Id = id;
        _domainNames = domainNames;
        Destination = destination;
        IsEnabled = isEnabled;
        Certificate = certificate;
    }

    public IReadOnlyList<string> DomainNames => _domainNames;
    public DestinationUri Destination { get; private set; }
    public bool IsEnabled { get; private set; }
    public ProxyCertificate? Certificate { get; private set; }

    public static ProxyHost Create(IEnumerable<string> domainNames, DestinationUri destination, ProxyCertificate? certificate = null)
    {
        ArgumentNullException.ThrowIfNull(domainNames);
        ArgumentNullException.ThrowIfNull(destination);

        var domains = domainNames.ToList();
        if (domains.Count == 0)
            throw new ArgumentException("At least one domain name is required.", nameof(domainNames));

        return new ProxyHost(Guid.NewGuid(), domains, destination, isEnabled: true, certificate);
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void UpdateDestination(DestinationUri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Destination = destination;
    }

    public void UpdateDomainNames(IEnumerable<string> domainNames)
    {
        ArgumentNullException.ThrowIfNull(domainNames);

        var domains = domainNames.ToList();
        if (domains.Count == 0)
            throw new ArgumentException("At least one domain name is required.", nameof(domainNames));

        _domainNames = domains;
    }

    public void SetCertificate(ProxyCertificate? certificate) => Certificate = certificate;
}
