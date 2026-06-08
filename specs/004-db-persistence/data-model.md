# Data Model: Database Persistence for Proxy Configuration

## Entities

### ProxyHost (existing aggregate — persisted)

Maps to PostgreSQL table `proxy_hosts`.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | Maps to `Entity.Id` |
| `domain_names` | `text[]` | NOT NULL, length ≥ 1 | Stored as PostgreSQL native array |
| `destination_scheme` | `varchar(5)` | NOT NULL | `"http"` or `"https"` |
| `destination_host` | `varchar(253)` | NOT NULL | RFC 1123 max hostname length |
| `destination_port` | `int` | NOT NULL, 1–65535 | |
| `is_enabled` | `boolean` | NOT NULL, default `true` | |
| `certificate_path` | `text` | NULL | Null when no custom cert |
| `certificate_key_path` | `text` | NULL | |
| `certificate_password` | `text` | NULL | |
| `created_at` | `timestamptz` | NOT NULL | Set once on insert |
| `updated_at` | `timestamptz` | NOT NULL | Updated on every write |

**Uniqueness**: No database-level unique index on `domain_names` array elements (enforced in application layer via `IProxyHostRepository.GetAllAsync` + conflict check, consistent with current behaviour). A future migration may add a GIN index for performance at scale.

**State transitions**:  
`IsEnabled: true ↔ false` (toggled via `ProxyHost.Enable()` / `ProxyHost.Disable()`). No delete-soft pattern; physical delete used (`RemoveAsync`).

---

### AuditLogEntry (existing record — persisted)

Maps to PostgreSQL table `audit_log_entries`.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `actor_id` | `varchar(256)` | NOT NULL | JWT `sub` claim or `"system"` |
| `operation` | `int` | NOT NULL | 0 = Created, 1 = Updated, 2 = Deleted |
| `proxy_host_id` | `uuid` | NOT NULL (no FK) | No FK — host may be deleted |
| `previous_state` | `text` | NULL | JSON snapshot of ProxyHostDto before change |
| `new_state` | `text` | NULL | JSON snapshot of ProxyHostDto after change |
| `occurred_at` | `timestamptz` | NOT NULL | UTC; indexed for retention purge and time-range queries |

**Retention**: Entries with `occurred_at < NOW() - retention_days` are purged by `AuditRetentionJob`. Default 90 days, configurable.

**Index**: `CREATE INDEX ix_audit_log_occurred_at ON audit_log_entries (occurred_at)` — supports retention purge and time-range filter queries.

**No FK to `proxy_hosts`**: Preserves audit history after a ProxyHost is deleted.

---

## Updated Repository Interfaces

### IAuditLogRepository

```csharp
public interface IAuditLogRepository
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(
        Guid proxyHostId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
```

`GetByProxyHostAsync` gains optional `from`/`to` parameters and pagination. `PurgeOlderThanAsync` returns the count of deleted rows for logging.

The existing `InMemoryAuditLogRepository` (used in unit tests) must be updated to match.

---

## DbContext

**Class**: `ProxyManagerDbContext` in `ProxyManager.Infrastructure.Data`

```csharp
public sealed class ProxyManagerDbContext(DbContextOptions<ProxyManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<ProxyHost> ProxyHosts => Set<ProxyHost>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProxyManagerDbContext).Assembly);
    }
}
```

EF entity configurations live in `Data/Configurations/`:
- `ProxyHostConfiguration : IEntityTypeConfiguration<ProxyHost>` — maps columns, `text[]`, owned `DestinationUri` and `ProxyCertificate` value objects.
- `AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>` — maps columns, `occurred_at` index.

---

## ProxyHost → YARP Translation

Performed by `ProxyHostYarpTranslator` (static class) in `ProxyManager` project. No DB access; pure mapping.

```text
ProxyHost record
  → RouteConfig  { RouteId = host.Id.ToString(),
                   ClusterId = host.Id.ToString(),
                   Match = { Hosts = host.DomainNames, Path = "/{**catch-all}" },
                   Order = 100 }
  → ClusterConfig { ClusterId = host.Id.ToString(),
                    Destinations = { "primary" → host.Destination.ToString() } }
```

Disabled hosts (`IsEnabled = false`) are excluded from the translated set.

---

## Configuration Options

### DatabaseOptions (ProxyManager.Infrastructure)
```csharp
public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public string ConnectionString { get; set; } = string.Empty;
}
```

### AuditOptions (ProxyManager.API)
```csharp
public sealed class AuditOptions
{
    public const string Section = "Audit";
    public int RetentionDays { get; set; } = 90;
}
```

Both sourced via `IOptions<T>` — never raw `IConfiguration` (constitution requirement).
