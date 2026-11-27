extern alias ProxyManagerApp;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LettuceEncrypt;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ProxyManagerApp::West94.ProxyManager.Acme;
using ProxyManagerApp::West94.ProxyManager.Options;
using West94.ProxyManager.API.Tests.Unit.Fakes;

namespace West94.ProxyManager.API.Tests.Unit.Acme;

[Trait("Category", "Unit")]
public sealed class FallbackCertificateSetupTests : IDisposable
{
    private const string ManagementHost = "manage.example.com";

    private readonly string contentRoot = Directory.CreateTempSubdirectory("fallback-cert-tests-").FullName;

    public void Dispose() => Directory.Delete(contentRoot, recursive: true);

    private FallbackCertificateSetup CreateSetup(FallbackCertificateOptions? fallback = null) => new(
        Microsoft.Extensions.Options.Options.Create(fallback ?? new FallbackCertificateOptions()),
        Microsoft.Extensions.Options.Options.Create(new ManagementOptions { Hosts = [ManagementHost] }),
        new TestHostEnvironment(contentRoot),
        NullLogger<FallbackCertificateSetup>.Instance);

    private string WriteFile(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(contentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] CreateCertOnlyPfx()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=nokey.example.com", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var withKey = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using var certOnly = X509CertificateLoader.LoadCertificate(withKey.RawData);
        return certOnly.Export(X509ContentType.Pfx);
    }

    [Fact]
    public void Configure_WithoutPath_LoadsManagementHostPfxFromCertsFolder()
    {
        WriteFile($"certs/{ManagementHost}.pfx", TestCertificateGenerator.CreatePfx($"CN={ManagementHost}"));
        var options = new LettuceEncryptOptions();

        CreateSetup().Configure(options);

        Assert.NotNull(options.FallbackCertificate);
        Assert.True(options.FallbackCertificate.HasPrivateKey);
        Assert.Equal($"CN={ManagementHost}", options.FallbackCertificate.Subject);
    }

    [Fact]
    public void Configure_WithRelativePathAndPassword_LoadsThatPfx()
    {
        WriteFile("tls/custom.pfx", TestCertificateGenerator.CreatePfx("CN=custom.example.com", "s3cret"));
        var options = new LettuceEncryptOptions();

        CreateSetup(new FallbackCertificateOptions { Path = "tls/custom.pfx", Password = "s3cret" }).Configure(options);

        Assert.Equal("CN=custom.example.com", options.FallbackCertificate?.Subject);
    }

    [Fact]
    public void Configure_WhenFileMissing_LeavesFallbackNull()
    {
        var options = new LettuceEncryptOptions();

        CreateSetup().Configure(options);

        Assert.Null(options.FallbackCertificate);
    }

    [Fact]
    public void Configure_WithWrongPassword_LeavesFallbackNull()
    {
        WriteFile($"certs/{ManagementHost}.pfx", TestCertificateGenerator.CreatePfx(password: "right"));
        var options = new LettuceEncryptOptions();

        CreateSetup(new FallbackCertificateOptions { Password = "wrong" }).Configure(options);

        Assert.Null(options.FallbackCertificate);
    }

    [Fact]
    public void Configure_WithPfxLackingPrivateKey_LeavesFallbackNull()
    {
        WriteFile($"certs/{ManagementHost}.pfx", CreateCertOnlyPfx());
        var options = new LettuceEncryptOptions();

        CreateSetup().Configure(options);

        Assert.Null(options.FallbackCertificate);
    }

    [Fact]
    public void Configure_WhenFallbackAlreadySet_KeepsIt()
    {
        WriteFile($"certs/{ManagementHost}.pfx", TestCertificateGenerator.CreatePfx($"CN={ManagementHost}"));
        using var existing = X509CertificateLoader.LoadPkcs12(TestCertificateGenerator.CreatePfx("CN=existing.example.com"), null);
        var options = new LettuceEncryptOptions { FallbackCertificate = existing };

        CreateSetup().Configure(options);

        Assert.Same(existing, options.FallbackCertificate);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "ProxyManager";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
