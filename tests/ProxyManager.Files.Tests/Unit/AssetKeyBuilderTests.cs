using West94.ProxyManager.Files.Assets;

namespace West94.ProxyManager.Files.Tests.Unit;

public sealed class AssetKeyBuilderTests
{
    [Fact]
    public void StagingKey_UsesUploadIdAndSanitizedFileName()
    {
        var uploadId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = AssetKeyBuilder.StagingKey(uploadId, "cert.pem");

        Assert.Equal("staging/11111111111111111111111111111111/cert.pem", key);
    }

    [Fact]
    public void CommittedKey_UsesAssetTypeAndAssetId()
    {
        var assetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var key = AssetKeyBuilder.CommittedKey("certificate", assetId, "cert.pem");

        Assert.Equal("certificate/22222222222222222222222222222222/cert.pem", key);
    }

    [Theory]
    [InlineData("../../etc/passwd", "etcpasswd")]
    [InlineData("a/b\\c", "abc")]
    [InlineData("", "asset.bin")]
    [InlineData("   ", "asset.bin")]
    public void SanitizeFileName_StripsTraversalAndSeparators(string input, string expected)
    {
        Assert.Equal(expected, AssetKeyBuilder.SanitizeFileName(input));
    }

    [Fact]
    public void SanitizeFileName_TruncatesToMaxLength()
    {
        var longName = new string('a', 300) + ".pem";
        var sanitized = AssetKeyBuilder.SanitizeFileName(longName);

        Assert.Equal(200, sanitized.Length);
    }

    [Fact]
    public void SanitizeFileName_StripsControlCharacters()
    {
        var bell = Convert.ToChar(7);
        var withControlChar = "cert" + bell + ".pem";
        var sanitized = AssetKeyBuilder.SanitizeFileName(withControlChar);

        Assert.Equal("cert.pem", sanitized);
    }

    [Theory]
    [InlineData("certificate", true)]
    [InlineData("Certificate", false)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void AssetTypeAllowlist_OnlyAllowsKnownLowercaseTypes(string assetType, bool expected)
    {
        Assert.Equal(expected, AssetTypeAllowlist.IsAllowed(assetType));
    }

    [Fact]
    public void AssetTypeAllowlist_Normalize_ThrowsForUnknownType()
    {
        Assert.Throws<FileAssetValidationException>(() => AssetTypeAllowlist.Normalize("unknown"));
    }

    [Fact]
    public void AssetTypeAllowlist_Normalize_LowercasesKnownType()
    {
        Assert.Equal("certificate", AssetTypeAllowlist.Normalize("Certificate"));
    }
}
