using West94.ProxyManager.Core.Messages.Events;
using West94.ProxyManager.Files.Services;

namespace West94.ProxyManager.Files.Integrations;

/// <summary>
/// Consumes <see cref="CertificateDeletedEvent"/> from the durable "certificates" exchange and
/// deletes the certificate's owned assets. A deliberate exception to "Files is domain-agnostic",
/// confined to this folder — the alternative (a synchronous DELETE call from the API) either
/// fails the user's delete or silently leaks the blob when Files/RustFS is down.
/// </summary>
public sealed class CertificateAssetCleanupHandler
{
    public async Task Handle(CertificateDeletedEvent e, IFileAssetService assets, CancellationToken ct) =>
        await assets.DeleteByOwnerAsync("certificate", e.Id, ct);
}
