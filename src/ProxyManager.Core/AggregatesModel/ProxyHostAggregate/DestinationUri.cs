namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public sealed record DestinationUri
{
    public string Scheme { get; }
    public string Host { get; }
    public int Port { get; }

    public DestinationUri(string scheme, string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");

        if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Scheme must be 'http' or 'https'.", nameof(scheme));

        Scheme = scheme.ToLowerInvariant();
        Host = host;
        Port = port;
    }

    public static DestinationUri Parse(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            throw new ArgumentException($"'{uri}' is not a valid absolute URI.", nameof(uri));

        int port = parsed.IsDefaultPort
            ? (parsed.Scheme == "https" ? 443 : 80)
            : parsed.Port;

        return new DestinationUri(parsed.Scheme, parsed.Host, port);
    }

    public override string ToString() => $"{Scheme}://{Host}:{Port}";
}
