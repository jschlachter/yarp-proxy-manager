using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

/// <summary>Builds valid <see cref="Certificate"/> instances for tests that don't care about the specific asset/subject values.</summary>
internal static class TestCertificates
{
    public static CertificateSubjectInfo MakeSubject(string subject = "CN=test.example.com") =>
        new(subject, ["test.example.com"], DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), "ABCDEF0123456789");

    public static Certificate Create(
        string name, CertificateFormat format,
        Guid? certificateAssetId = null, Guid? keyAssetId = null,
        string certificateFileName = "cert.pem", string? keyFileName = null,
        string? passPhrase = null) =>
        Certificate.Create(
            name, format, certificateAssetId ?? Guid.NewGuid(), keyAssetId,
            certificateFileName, keyFileName, passPhrase, MakeSubject());
}
