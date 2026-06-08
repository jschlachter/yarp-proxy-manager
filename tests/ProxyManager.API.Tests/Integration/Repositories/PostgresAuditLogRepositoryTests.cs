using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Infrastructure.Data;
using West94.ProxyManager.Infrastructure.Extensions;
using West94.ProxyManager.Infrastructure.Options;

namespace West94.ProxyManager.API.Tests.Integration.Repositories;

public sealed class PostgresAuditLogRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("proxymanager_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private IServiceScope _scope = null!;
    private IAuditLogRepository _repo = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<DatabaseOptions>>(
            new OptionsWrapper<DatabaseOptions>(new DatabaseOptions { ConnectionString = _postgres.GetConnectionString() }));
        services.AddProxyManagerInfrastructure();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();

        var db = _scope.ServiceProvider.GetRequiredService<ProxyManagerDbContext>();
        await db.Database.MigrateAsync();

        _repo = _scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope.Dispose();
        await _postgres.DisposeAsync();
    }

    private static AuditLogEntry MakeEntry(Guid proxyHostId, AuditOperation op = AuditOperation.Created,
        DateTimeOffset? at = null)
    {
        var entry = AuditLogEntry.Create("actor-test", op, proxyHostId, null, null);
        return at is null ? entry : entry with { OccurredAt = at.Value };
    }

    [Fact]
    public async Task AppendAsync_And_GetByProxyHostAsync_RoundTrip()
    {
        var id = Guid.NewGuid();
        var entry = MakeEntry(id);

        await _repo.AppendAsync(entry);
        var results = await _repo.GetByProxyHostAsync(id);

        Assert.Single(results);
        Assert.Equal(id, results[0].ProxyHostId);
        Assert.Equal("actor-test", results[0].ActorId);
    }

    [Fact]
    public async Task GetByProxyHostAsync_FiltersByProxyHostId()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await _repo.AppendAsync(MakeEntry(id1));
        await _repo.AppendAsync(MakeEntry(id2));

        var results = await _repo.GetByProxyHostAsync(id1);

        Assert.Single(results);
        Assert.Equal(id1, results[0].ProxyHostId);
    }

    [Fact]
    public async Task GetByProxyHostAsync_FromFilter_ExcludesEarlierEntries()
    {
        var id = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddHours(-2);
        var t1 = DateTimeOffset.UtcNow.AddHours(-1);
        var t2 = DateTimeOffset.UtcNow;
        await _repo.AppendAsync(MakeEntry(id, at: t0));
        await _repo.AppendAsync(MakeEntry(id, at: t1));
        await _repo.AppendAsync(MakeEntry(id, at: t2));

        var results = await _repo.GetByProxyHostAsync(id, from: t1);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetByProxyHostAsync_ToFilter_ExcludesLaterEntries()
    {
        var id = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddHours(-2);
        var t1 = DateTimeOffset.UtcNow.AddHours(-1);
        var t2 = DateTimeOffset.UtcNow;
        await _repo.AppendAsync(MakeEntry(id, at: t0));
        await _repo.AppendAsync(MakeEntry(id, at: t1));
        await _repo.AppendAsync(MakeEntry(id, at: t2));

        var results = await _repo.GetByProxyHostAsync(id, to: t1);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetByProxyHostAsync_Pagination_ReturnsCorrectPage()
    {
        var id = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
            await _repo.AppendAsync(MakeEntry(id));

        var page1 = await _repo.GetByProxyHostAsync(id, page: 1, pageSize: 2);
        var page2 = await _repo.GetByProxyHostAsync(id, page: 2, pageSize: 2);
        var page3 = await _repo.GetByProxyHostAsync(id, page: 3, pageSize: 2);

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Single(page3);
    }

    [Fact]
    public async Task PurgeOlderThanAsync_DeletesOnlyOldEntries()
    {
        var id = Guid.NewGuid();
        var old = DateTimeOffset.UtcNow.AddDays(-100);
        var recent = DateTimeOffset.UtcNow;
        await _repo.AppendAsync(MakeEntry(id, at: old));
        await _repo.AppendAsync(MakeEntry(id, at: recent));

        var cutoff = DateTimeOffset.UtcNow.AddDays(-91);
        var deleted = await _repo.PurgeOlderThanAsync(cutoff);

        Assert.Equal(1, deleted);
        var remaining = await _repo.GetByProxyHostAsync(id);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPaginatedEntriesDescending()
    {
        for (var i = 0; i < 3; i++)
            await _repo.AppendAsync(MakeEntry(Guid.NewGuid()));

        var page1 = await _repo.GetAllAsync(1, 2);

        Assert.Equal(2, page1.Count);
    }
}
