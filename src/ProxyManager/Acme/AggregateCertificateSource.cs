using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using LettuceEncrypt;
using Microsoft.Extensions.Logging;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Infrastructure.Files;
using CoreCertificateRepository = West94.ProxyManager.Core.AggregatesModel.CertificateAggregate.ICertificateRepository;

namespace West94.ProxyManager.Acme;

/// <summary>
/// Loads pre-existing certificates (manual or Let's Encrypt-issued) from the <c>Certificate</c>
/// aggregate for LettuceEncrypt: at startup (<see cref="GetCertificatesAsync"/>, every stored row)
/// and on-demand per handshake (<see cref="GetCertificateAsync"/>, filtered by SAN). Registered as a
/// singleton, so it resolves its own DI scope per call for the scoped <see cref="ICertificateRepository"/>.
/// </summary>
public sealed class AggregateCertificateSource(
    IServiceScopeFactory scopeFactory,
    ILogger<AggregateCertificateSource> logger) : ICertificateSource
{
    public async Task<IEnumerable<X509Certificate2>> GetCertificatesAsync(CancellationToken cancellationToken)
    {
        var certificates = await LoadAllAsync(cancellationToken);

        var loaded = new List<X509Certificate2>();
        foreach (var certificate in certificates)
        {
            var x509 = await TryLoadAsync(certificate, cancellationToken);
            if (x509 is not null)
            {
                loaded.Add(x509);
            }
        }

        return loaded;
    }

    public async Task<X509Certificate2?> GetCertificateAsync(string domainName, CancellationToken cancellationToken)
    {
        var certificates = await LoadAllAsync(cancellationToken);

        var match = certificates.FirstOrDefault(c =>
            c.Subject.SubjectAlternativeNames.Any(san => MatchesDomain(san, domainName)));

        return match is null ? null : await TryLoadAsync(match, cancellationToken);
    }

    /// <summary>
    /// SNI hostname match against a stored SAN entry, supporting a single-label wildcard
    /// (RFC 6125 §6.4.3 "left-most label" style: <c>*.example.com</c> matches <c>foo.example.com</c>
    /// but not <c>example.com</c> or <c>foo.bar.example.com</c>) since manually-uploaded certificates
    /// (e.g. wildcard certs) commonly rely on this, unlike Let's Encrypt HTTP-01 certs which are always
    /// exact-domain (see plan non-goals — no wildcard issuance, only wildcard matching for Manual certs).
    /// </summary>
    private static bool MatchesDomain(string san, string domainName)
    {
        if (string.Equals(san, domainName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!san.StartsWith("*.", StringComparison.Ordinal))
        {
            return false;
        }

        var sanSuffix = san[1..]; // ".example.com"
        var domainFirstDot = domainName.IndexOf('.');
        if (domainFirstDot < 0)
        {
            return false;
        }

        var domainSuffix = domainName[domainFirstDot..]; // ".foo.example.com" -> ".example.com" only if single-label left of it
        return string.Equals(sanSuffix, domainSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<Certificate>> LoadAllAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<CoreCertificateRepository>();
        return await repository.GetAllAsync(ct);
    }

    private async Task<X509Certificate2?> TryLoadAsync(Certificate certificate, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IFileAssetClient>();

            var certBytes = await files.GetContentAsync(certificate.CertificateAssetId, ct);

            return certificate.Format switch
            {
                CertificateFormat.Pfx => X509CertificateLoader.LoadPkcs12(certBytes, certificate.PassPhrase),
                CertificateFormat.Pem => await LoadPemAsync(files, certificate, certBytes, ct),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping certificate {CertificateId} ({Name}) — failed to load.", certificate.Id, certificate.Name);
            return null;
        }
    }

    private static async Task<X509Certificate2?> LoadPemAsync(IFileAssetClient files, Certificate certificate, byte[] certBytes, CancellationToken ct)
    {
        if (certificate.KeyAssetId is null)
        {
            return null;
        }

        var keyBytes = await files.GetContentAsync(certificate.KeyAssetId.Value, ct);
        var certPem = System.Text.Encoding.UTF8.GetString(certBytes);
        var keyPem = System.Text.Encoding.UTF8.GetString(keyBytes);

        return string.IsNullOrEmpty(certificate.PassPhrase)
            ? X509Certificate2.CreateFromPem(certPem, keyPem)
            : X509Certificate2.CreateFromEncryptedPem(certPem, keyPem, certificate.PassPhrase);
    }
}
