using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public class ProxyHost : Entity
{
    private List<string> _domainNames;

    private ProxyHost(Guid id, List<string> domainNames, DestinationUri destination, bool isEnabled, Guid? certificateId)
    {
        Id = id;
        _domainNames = domainNames;
        Destination = destination;
        IsEnabled = isEnabled;
        CertificateId = certificateId;
    }

    public IReadOnlyList<string> DomainNames => _domainNames;
    public DestinationUri Destination { get; private set; }
    public bool IsEnabled { get; private set; }
    public Guid? CertificateId { get; private set; }

    /// <summary>Reconstitutes a ProxyHost from its persisted state. For Infrastructure layer use only.</summary>
    internal static ProxyHost Reconstitute(Guid id, IEnumerable<string> domainNames, DestinationUri destination, bool isEnabled, Guid? certificateId) =>
        new(id, domainNames.ToList(), destination, isEnabled, certificateId);

    public static ProxyHost Create(IEnumerable<string> domainNames, DestinationUri destination, Guid? certificateId = null)
    {
        ArgumentNullException.ThrowIfNull(domainNames);
        ArgumentNullException.ThrowIfNull(destination);

        var domains = domainNames.ToList();
        if (domains.Count == 0)
            throw new ArgumentException("At least one domain name is required.", nameof(domainNames));

        return new ProxyHost(Guid.NewGuid(), domains, destination, isEnabled: true, certificateId);
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

    public void AssignCertificate(Guid? certificateId) => CertificateId = certificateId;
}
