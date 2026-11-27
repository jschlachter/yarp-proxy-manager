namespace West94.ProxyManager.Acme;

/// <summary>
/// Locates the PFX served when LettuceEncrypt has no certificate for a TLS handshake: no SNI, or a
/// host with no stored or issued certificate. Above all it covers the management host, which can't
/// be given a certificate through the UI/API while the UI is only reachable over HTTPS on that host.
/// </summary>
public sealed class FallbackCertificateOptions
{
    public const string Section = "FallbackCertificate";

    /// <summary>
    /// Path to the PFX, absolute or relative to the content root. Unset, it defaults to
    /// <c>certs/{first Management:Hosts entry}.pfx</c>, the file the Quadlet unit mounts.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>The PFX's password; unset or empty for an unprotected PFX.</summary>
    public string? Password { get; set; }
}
