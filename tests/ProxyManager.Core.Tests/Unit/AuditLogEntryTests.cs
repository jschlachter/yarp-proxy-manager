using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

namespace West94.ProxyManager.Core.Tests.Unit;

public class AuditLogEntryTests
{
    [Fact]
    public void Create_SetsIdAndOccurredAtInUtc()
    {
        var before = DateTimeOffset.UtcNow;

        var entry = AuditLogEntry.Create("actor-1", AuditOperation.Created, Guid.NewGuid(), null, null);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.True(entry.OccurredAt >= before);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
    }

    [Fact]
    public void Create_SetsActorIdCorrectly()
    {
        var entry = AuditLogEntry.Create("user-abc", AuditOperation.Created, Guid.NewGuid(), null, null);

        Assert.Equal("user-abc", entry.ActorId);
    }

    [Fact]
    public void Create_EmptyActorId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(string.Empty, AuditOperation.Created, Guid.NewGuid(), null, null));
    }

    [Fact]
    public void Create_WithCreatedOperation_HasNullPreviousState()
    {
        var entry = AuditLogEntry.Create("actor-1", AuditOperation.Created, Guid.NewGuid(), null, null);

        Assert.Null(entry.PreviousState);
    }

    [Fact]
    public void Create_WithDeletedOperation_HasNullNewState()
    {
        var entry = AuditLogEntry.Create("actor-1", AuditOperation.Deleted, Guid.NewGuid(), "{}", null);

        Assert.Null(entry.NewState);
    }

    [Fact]
    public void Create_WithUpdatedOperation_HasBothStates()
    {
        var entry = AuditLogEntry.Create("actor-1", AuditOperation.Updated, Guid.NewGuid(), "{\"old\":1}", "{\"new\":2}");

        Assert.NotNull(entry.PreviousState);
        Assert.NotNull(entry.NewState);
    }
}
