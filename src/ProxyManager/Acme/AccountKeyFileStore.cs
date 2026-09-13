using System.Text.Json;

using LettuceEncrypt.Accounts;

namespace West94.ProxyManager.Acme;

/// <summary>
/// Persists only the ACME account key (not certificates) to a single JSON file so the
/// account survives container restarts. Deliberately avoids LettuceEncrypt's built-in
/// <c>PersistDataToDirectory</c>/<c>FileSystemAccountStore</c>, whose <c>FileSystemCertificateRepository</c>
/// also registers itself as an <c>ICertificateRepository</c>/<c>ICertificateSource</c> — duplicating every
/// issued certificate's full PFX (private key included) outside the <c>Certificate</c> aggregate's own
/// lifecycle. Certificates are already durably stored/loaded through the aggregate via
/// <see cref="AggregateCertificateSource"/> and <see cref="AggregateCertificateRepository"/>, so only the
/// account key needs its own persistence here.
/// </summary>
public sealed class AccountKeyFileStore : IAccountStore
{
    private readonly string _filePath;

    public AccountKeyFileStore(DirectoryInfo directory)
    {
        Directory.CreateDirectory(directory.FullName);
        _filePath = Path.Combine(directory.FullName, "acme-account.json");
    }

    public async Task SaveAccountAsync(AccountModel account, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, account, cancellationToken: cancellationToken);
    }

    public async Task<AccountModel?> GetAccountAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<AccountModel>(stream, cancellationToken: cancellationToken);
    }
}
