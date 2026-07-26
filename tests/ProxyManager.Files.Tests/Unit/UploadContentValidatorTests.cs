using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Validation;

namespace West94.ProxyManager.Files.Tests.Unit;

public sealed class UploadContentValidatorTests
{
    private static readonly byte[] PemHeader = "-----BEGIN CERTIFICATE-----"u8.ToArray();
    private static readonly byte[] DerHeader = [0x30, 0x82, 0x01, 0x02];
    private static readonly byte[] JunkHeader = "not a certificate"u8.ToArray();

    private static UploadContentValidator CreateValidator() =>
        new(Microsoft.Extensions.Options.Options.Create(new UploadOptions()));

    [Fact]
    public void Validate_AcceptsPemHeader_ForAllowedExtension()
    {
        var contentType = CreateValidator().Validate("certificate", "cert.pem", PemHeader);
        Assert.Equal("application/x-pem-file", contentType);
    }

    [Fact]
    public void Validate_AcceptsDerHeader_ForPfxExtension()
    {
        var contentType = CreateValidator().Validate("certificate", "bundle.pfx", DerHeader);
        Assert.Equal("application/x-pkcs12", contentType);
    }

    [Fact]
    public void Validate_AcceptsDerHeader_ForCrtExtension_AsOctetStream()
    {
        var contentType = CreateValidator().Validate("certificate", "cert.crt", DerHeader);
        Assert.Equal("application/octet-stream", contentType);
    }

    [Fact]
    public void Validate_Throws_ForDisallowedExtension()
    {
        Assert.Throws<UnsupportedAssetContentException>(() =>
            CreateValidator().Validate("certificate", "cert.txt", PemHeader));
    }

    [Fact]
    public void Validate_Throws_ForUnrecognizedAssetType()
    {
        Assert.Throws<UnsupportedAssetContentException>(() =>
            CreateValidator().Validate("widget", "cert.pem", PemHeader));
    }

    [Fact]
    public void Validate_Throws_WhenHeaderMatchesNeitherPemNorDer()
    {
        Assert.Throws<UnsupportedAssetContentException>(() =>
            CreateValidator().Validate("certificate", "cert.pem", JunkHeader));
    }

    [Fact]
    public void Validate_NeverTrustsClaimedExtensionAlone_WhenBytesDoNotMatch()
    {
        // A ".pem" file whose bytes are neither PEM nor DER must still be rejected.
        Assert.Throws<UnsupportedAssetContentException>(() =>
            CreateValidator().Validate("certificate", "fake.pem", []));
    }
}
