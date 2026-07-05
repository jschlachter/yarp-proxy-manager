using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Infrastructure.Data;
using West94.ProxyManager.Infrastructure.Extensions;
using West94.ProxyManager.Infrastructure.Options;
using West94.ProxyManager.Infrastructure.Repositories;

namespace West94.ProxyManager.API.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class PostgresProxyHostRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("proxymanager_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private IServiceScope _scope = null!;
    private IProxyHostRepository _repo = null!;

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

        _repo = _scope.ServiceProvider.GetRequiredService<IProxyHostRepository>();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope.Dispose();
        await _postgres.DisposeAsync();
    }

    private static ProxyHost MakeHost(string domain = "test.example.com") =>
        ProxyHost.Create([domain], DestinationUri.Parse("http://backend:8080"));

    [Fact]
    public async Task AddAsync_And_FindAsync_RoundTrip()
    {
        var host = MakeHost("roundtrip.example.com");

        await _repo.AddAsync(host);
        var loaded = await _repo.FindAsync(host.Id);

        Assert.NotNull(loaded);
        Assert.Equal(host.Id, loaded.Id);
        Assert.Contains("roundtrip.example.com", loaded.DomainNames);
        Assert.Equal(host.Destination.ToString(), loaded.Destination.ToString());
        Assert.True(loaded.IsEnabled);
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        var result = await _repo.FindAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllHosts()
    {
        await _repo.AddAsync(MakeHost("all1.example.com"));
        await _repo.AddAsync(MakeHost("all2.example.com"));

        var all = await _repo.GetAllAsync();

        Assert.True(all.Count >= 2);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var host = MakeHost("update.example.com");
        await _repo.AddAsync(host);

        host.Disable();
        await _repo.UpdateAsync(host);

        var loaded = await _repo.FindAsync(host.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsEnabled);
    }

    [Fact]
    public async Task RemoveAsync_DeletesRecord()
    {
        var host = MakeHost("remove.example.com");
        await _repo.AddAsync(host);

        await _repo.RemoveAsync(host.Id);
        var loaded = await _repo.FindAsync(host.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentId_DoesNotThrow()
    {
        await _repo.RemoveAsync(Guid.NewGuid()); // should not throw
    }

    [Fact]
    public async Task AddAsync_DuplicateId_ThrowsInvalidOperation()
    {
        var host = MakeHost("dupid.example.com");
        await _repo.AddAsync(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.AddAsync(host));
    }
}
