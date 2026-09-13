using System.Security.Cryptography.X509Certificates;

using LettuceEncrypt;
using Microsoft.Extensions.Logging;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Certificates;
using West94.ProxyManager.Infrastructure.Files;
using CoreCertificateRepository = West94.ProxyManager.Core.AggregatesModel.CertificateAggregate.ICertificateRepository;

namespace West94.ProxyManager.Acme;

/// <summary>
/// LettuceEncrypt's persistence hook: called whenever a certificate is issued or renewed. Persists
/// the bytes into ProxyManager.Files and the metadata into the <c>Certificate</c> aggregate — reusing
/// it in place (<see cref="Certificate.ReplaceAssets"/>) on renewal so <c>Certificate.Id</c> and any
/// <c>ProxyHost.CertificateId</c> pointing at it survive — then keeps the matching <c>ProxyHost</c>'s
/// <c>CertificateId</c> in sync. Registered as a singleton, so it resolves its own DI scope per call.
/// </summary>
public sealed class AggregateCertificateRepository(
    IServiceScopeFactory scopeFactory,
    ILogger<AggregateCertificateRepository> logger) : LettuceEncrypt.ICertificateRepository
{
    public async Task SaveAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var certificates = scope.ServiceProvider.GetRequiredService<CoreCertificateRepository>();
        var proxyHosts = scope.ServiceProvider.GetRequiredService<IProxyHostRepository>();
        var files = scope.ServiceProvider.GetRequiredService<IFileAssetClient>();

        var pfxBytes = certificate.Export(X509ContentType.Pfx);
        var subject = X509CertificateInspector.Inspect(pfxBytes, ReadOnlySpan<byte>.Empty, CertificateFormat.Pfx, passPhrase: null);

        var sanSet = new HashSet<string>(subject.SubjectAlternativeNames, StringComparer.OrdinalIgnoreCase);
        var existing = (await certificates.GetAllAsync(cancellationToken))
            .FirstOrDefault(c => c.Source == CertificateSource.LetsEncrypt &&
                                  sanSet.SetEquals(c.Subject.SubjectAlternativeNames));

        var fileName = $"{(sanSet.Count > 0 ? sanSet.First() : "certificate")}.pfx";
        using var content = new MemoryStream(pfxBytes);
        var assetId = await files.UploadAsync(fileName, "application/x-pkcs12", content, cancellationToken);

        Certificate result;
        if (existing is not null)
        {
            existing.ReplaceAssets(assetId, keyAssetId: null, fileName, keyFileName: null, subject);
            await certificates.UpdateAsync(existing, cancellationToken);
            result = existing;
            logger.LogInformation("Renewed Let's Encrypt certificate {CertificateId} for {Domains}.", result.Id, string.Join(", ", sanSet));
        }
        else
        {
            result = Certificate.Create(
                name: sanSet.Count > 0 ? sanSet.First() : "letsencrypt-certificate",
                CertificateFormat.Pfx, assetId, keyAssetId: null, fileName, keyFileName: null,
                passPhrase: null, subject, source: CertificateSource.LetsEncrypt);
            await certificates.AddAsync(result, cancellationToken);
            logger.LogInformation("Issued new Let's Encrypt certificate {CertificateId} for {Domains}.", result.Id, string.Join(", ", sanSet));
        }

        await files.CommitAsync(assetId, "certificate", result.Id, cancellationToken);

        var hosts = await proxyHosts.GetAllAsync(cancellationToken);
        foreach (var host in hosts.Where(h => h.TlsMode == TlsMode.LetsEncrypt && sanSet.SetEquals(h.DomainNames)))
        {
            if (host.CertificateId == result.Id)
            {
                continue;
            }

            host.AssignCertificate(result.Id);
            await proxyHosts.UpdateAsync(host, cancellationToken);
        }
    }
}
