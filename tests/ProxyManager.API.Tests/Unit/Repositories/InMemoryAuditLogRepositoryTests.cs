using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Infrastructure.Repositories;

namespace West94.ProxyManager.API.Tests.Unit.Repositories;

public class InMemoryAuditLogRepositoryTests
{
    private static AuditLogEntry MakeEntry(Guid proxyHostId, AuditOperation op = AuditOperation.Created) =>
        AuditLogEntry.Create("actor-1", op, proxyHostId, null, null);

    [Fact]
    public async Task AppendAsync_AddsEntryToStore()
    {
        var repo = new InMemoryAuditLogRepository();
        var id = Guid.NewGuid();

        await repo.AppendAsync(MakeEntry(id));

        var results = await repo.GetByProxyHostAsync(id);
        Assert.Single(results);
    }

    [Fact]
    public async Task GetByProxyHostAsync_ReturnsOnlyMatchingEntries()
    {
        var repo = new InMemoryAuditLogRepository();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await repo.AppendAsync(MakeEntry(id1));
        await repo.AppendAsync(MakeEntry(id2));

        var results = await repo.GetByProxyHostAsync(id1);

        Assert.Single(results);
        Assert.Equal(id1, results[0].ProxyHostId);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPaginatedEntries()
    {
        var repo = new InMemoryAuditLogRepository();
        for (var i = 0; i < 5; i++)
            await repo.AppendAsync(MakeEntry(Guid.NewGuid()));

        var page1 = await repo.GetAllAsync(1, 3);
        var page2 = await repo.GetAllAsync(2, 3);

        Assert.Equal(3, page1.Count);
        Assert.Equal(2, page2.Count);
    }

    [Fact]
    public async Task GetAllAsync_EmptyStore_ReturnsEmptyList()
    {
        var repo = new InMemoryAuditLogRepository();

        var results = await repo.GetAllAsync(1, 20);

        Assert.Empty(results);
    }
}
