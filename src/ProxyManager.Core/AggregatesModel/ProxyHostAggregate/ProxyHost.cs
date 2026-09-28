using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public class ProxyHost : Entity
{
    private List<string> _domainNames;

    private ProxyHost(Guid id, List<string> domainNames, DestinationUri destination, bool isEnabled, Guid? certificateId, TlsMode tlsMode, HealthCheckSettings? healthCheck)
    {
        Id = id;
        _domainNames = domainNames;
        Destination = destination;
        IsEnabled = isEnabled;
        CertificateId = certificateId;
        TlsMode = tlsMode;
        HealthCheck = healthCheck;
    }

    public IReadOnlyList<string> DomainNames => _domainNames;
    public DestinationUri Destination { get; private set; }
    public bool IsEnabled { get; private set; }
    public Guid? CertificateId { get; private set; }
    public TlsMode TlsMode { get; private set; }

    /// <summary>YARP health checks for this host; null when no check is enabled (ADR 0003).</summary>
    public HealthCheckSettings? HealthCheck { get; private set; }

    /// <summary>Reconstitutes a ProxyHost from its persisted state. For Infrastructure layer use only.</summary>
    internal static ProxyHost Reconstitute(Guid id, IEnumerable<string> domainNames, DestinationUri destination, bool isEnabled, Guid? certificateId, TlsMode tlsMode, HealthCheckSettings? healthCheck = null) =>
        new(id, domainNames.ToList(), destination, isEnabled, certificateId, tlsMode, healthCheck);

    public static ProxyHost Create(IEnumerable<string> domainNames, DestinationUri destination, Guid? certificateId = null, TlsMode tlsMode = TlsMode.Manual, HealthCheckSettings? healthCheck = null)
    {
        ArgumentNullException.ThrowIfNull(domainNames);
        ArgumentNullException.ThrowIfNull(destination);

        var domains = domainNames.ToList();
        if (domains.Count == 0)
            throw new ArgumentException("At least one domain name is required.", nameof(domainNames));

        return new ProxyHost(Guid.NewGuid(), domains, destination, isEnabled: true, certificateId, tlsMode, healthCheck);
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

    public void SetTlsMode(TlsMode mode) => TlsMode = mode;

    /// <summary>Replaces the health check settings; null turns every check off.</summary>
    public void ConfigureHealthCheck(HealthCheckSettings? settings) => HealthCheck = settings;
}
