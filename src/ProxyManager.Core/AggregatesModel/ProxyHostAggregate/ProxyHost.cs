namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public sealed class ProxyHost
{
    private List<string> _domainNames;

    private ProxyHost(Guid id, List<string> domainNames, string destinationAddress, bool isEnabled, ProxyCertificate? certificate)
    {
        Id = id;
        _domainNames = domainNames;
        DestinationAddress = destinationAddress;
        IsEnabled = isEnabled;
        Certificate = certificate;
    }

    public Guid Id { get; private set; }
    public IReadOnlyList<string> DomainNames => _domainNames;
    public string DestinationAddress { get; private set; }
    public bool IsEnabled { get; private set; }
    public ProxyCertificate? Certificate { get; private set; }

    public static ProxyHost Create(IEnumerable<string> domainNames, string destinationAddress, ProxyCertificate? certificate = null)
    {
        ArgumentNullException.ThrowIfNull(domainNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationAddress);

        var domains = domainNames.ToList();
        if (domains.Count == 0)
            throw new ArgumentException("At least one domain name is required.", nameof(domainNames));

        return new ProxyHost(Guid.NewGuid(), domains, destinationAddress, isEnabled: true, certificate);
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void UpdateDestination(string destinationAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationAddress);
        DestinationAddress = destinationAddress;
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
