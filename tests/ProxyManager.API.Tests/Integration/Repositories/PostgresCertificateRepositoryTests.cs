using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Infrastructure.Data;
using West94.ProxyManager.Infrastructure.Extensions;
using West94.ProxyManager.Infrastructure.Options;

namespace West94.ProxyManager.API.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class PostgresCertificateRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("proxymanager_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private IServiceScope _scope = null!;
    private ICertificateRepository _repo = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<DatabaseOptions>>(
            new OptionsWrapper<DatabaseOptions>(new DatabaseOptions { ConnectionString = _postgres.GetConnectionString() }));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddProxyManagerInfrastructure();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();

        var db = _scope.ServiceProvider.GetRequiredService<ProxyManagerDbContext>();
        await db.Database.MigrateAsync();

        _repo = _scope.ServiceProvider.GetRequiredService<ICertificateRepository>();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope.Dispose();
        await _postgres.DisposeAsync();
    }

    private static Certificate MakePemCert(string name = "test-pem") =>
        Certificate.Create(name, CertificateFormat.Pem, $"/certs/{name}.pem", $"/certs/{name}.key", "pass");

    private static Certificate MakePfxCert(string name = "test-pfx") =>
        Certificate.Create(name, CertificateFormat.Pfx, $"/certs/{name}.pfx");

    [Fact]
    public async Task AddAsync_And_FindAsync_RoundTrip_Pem()
    {
        var cert = MakePemCert("roundtrip-pem");

        await _repo.AddAsync(cert);
        var loaded = await _repo.FindAsync(cert.Id);

        Assert.NotNull(loaded);
        Assert.Equal(cert.Id, loaded.Id);
        Assert.Equal("roundtrip-pem", loaded.Name);
        Assert.Equal(CertificateFormat.Pem, loaded.Format);
        Assert.Equal("/certs/roundtrip-pem.pem", loaded.CertificatePath);
        Assert.Equal("/certs/roundtrip-pem.key", loaded.KeyFilePath);
    }

    [Fact]
    public async Task AddAsync_And_FindAsync_RoundTrip_Pfx()
    {
        var cert = MakePfxCert("roundtrip-pfx");

        await _repo.AddAsync(cert);
        var loaded = await _repo.FindAsync(cert.Id);

        Assert.NotNull(loaded);
        Assert.Equal(CertificateFormat.Pfx, loaded.Format);
        Assert.Null(loaded.KeyFilePath);
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        var result = await _repo.FindAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCerts()
    {
        await _repo.AddAsync(MakePemCert("all1"));
        await _repo.AddAsync(MakePfxCert("all2"));

        var all = await _repo.GetAllAsync();

        Assert.True(all.Count >= 2);
    }

    [Fact]
    public async Task UpdateAsync_PersistsRename()
    {
        var cert = MakePemCert("before-rename");
        await _repo.AddAsync(cert);

        cert.Rename("after-rename");
        await _repo.UpdateAsync(cert);

        var loaded = await _repo.FindAsync(cert.Id);
        Assert.NotNull(loaded);
        Assert.Equal("after-rename", loaded.Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsPassPhraseChange()
    {
        var cert = MakePfxCert("passphrase-test");
        await _repo.AddAsync(cert);

        cert.UpdatePassPhrase("new-secret");
        await _repo.UpdateAsync(cert);

        var loaded = await _repo.FindAsync(cert.Id);
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task RemoveAsync_DeletesRecord()
    {
        var cert = MakePemCert("to-delete");
        await _repo.AddAsync(cert);

        await _repo.RemoveAsync(cert.Id);
        var loaded = await _repo.FindAsync(cert.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentId_DoesNotThrow()
    {
        await _repo.RemoveAsync(Guid.NewGuid());
    }
}
