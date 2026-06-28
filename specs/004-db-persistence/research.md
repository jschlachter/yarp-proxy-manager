# Research: Database Persistence for Proxy Configuration

## 1. ORM / Data Access

**Decision**: EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10  
**Rationale**: EF Core is the canonical .NET data-access library, already part of the .NET 10 ecosystem. Npgsql's EF Core provider has first-class `text[]` column support for mapping `IReadOnlyList<string>` (ProxyHost.DomainNames) and full async support. Migrations via `dotnet ef migrations add` produce reproducible schema changes that apply automatically at startup via `MigrateAsync()`.  
**Alternatives considered**:  
- *Dapper* — lighter, but requires hand-written SQL and manual migration tooling; not justified when the domain model already uses EF-compatible aggregate roots.  
- *Raw ADO.NET* — excessive boilerplate; no migration support.

---

## 2. YARP Live-Reload Strategy

**Decision**: Custom `IProxyConfigProvider` backed by the database + RabbitMQ event-driven invalidation via a Wolverine consumer in ProxyManager.  
**Rationale**: YARP's `IProxyConfigProvider` is the official extension point for dynamic routing. The interface returns `IProxyConfig` whose `ChangeToken` signals YARP to reload. On startup, `DatabaseProxyConfigProvider` reads all enabled ProxyHost records and translates them to YARP `RouteConfig`/`ClusterConfig` objects. When `ProxyHostCreatedEvent`, `ProxyHostUpdatedEvent`, or `ProxyHostDeletedEvent` arrives via RabbitMQ, a Wolverine handler calls `DatabaseProxyConfigProvider.Reload()` which re-queries the database and fires a new `CancellationChangeToken`, triggering YARP to call `GetConfig()` again.  
**Alternatives considered**:  
- *Polling (IHostedService)* — simpler (no extra package in ProxyManager), but introduces up to N-second lag and wastes DB queries when nothing changes. Rejected in favour of event-driven approach.  
- *HTTP callback from API to ProxyManager* — tight coupling between services; introduces a synchronous dependency that breaks if ProxyManager is restarting. Rejected.

---

## 3. System Routes vs. ProxyHost Routes

**Decision**: `proxysettings.{Environment}.json` is retained for **system routes only** (apiRoute, ui-api-route, ui-route). User-managed ProxyHost routes are served exclusively by `DatabaseProxyConfigProvider`. YARP allows multiple `IProxyConfigProvider` registrations; routes from both are merged at runtime.  
**Rationale**: System routes (to ProxyManager.API, to the UI frontend) are infrastructure concerns that don't belong in the ProxyHost abstraction. They have fixed transforms, authorization policies, and targets that operators should not accidentally delete through the admin UI. Keeping them in the file avoids polluting the ProxyHost domain with system internals.  
**Alternatives considered**:  
- *Migrate all routes to DB, including system routes* — risks operator error deleting the /api/** route; seeding logic becomes non-trivial. Rejected.

---

## 4. Domain Names Column Storage

**Decision**: PostgreSQL `text[]` (native array column) mapped via Npgsql's `HasColumnType("text[]")`.  
**Rationale**: `ProxyHost.DomainNames` is a value-typed list with no independent identity; a separate join table adds query complexity for no benefit. Npgsql maps `List<string>` / `string[]` to `text[]` transparently. EF Core owned-entity JSON (`ToJson()`) is an alternative but requires .NET 8+ JSON column support which behaves differently across providers.  
**Alternatives considered**:  
- *Separate `ProxyHostDomains` table* — unnecessary normalisation for a simple string list. Rejected.  
- *EF Core JSON column (`ToJson()`)* — `text[]` is simpler and more queryable in PostgreSQL. Rejected.

---

## 5. Audit Log Time-Range Query and Retention

**Decision**: Extend `IAuditLogRepository` with:
- `GetByProxyHostAsync(Guid proxyHostId, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct)` — filtered + paginated.
- `PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)` — bulk delete for retention.

Retention is enforced by an `IHostedService` (`AuditRetentionJob`) in `ProxyManager.API` that runs once per day at startup and then on a 24-hour timer. The retention window (default 90 days) is read from `IOptions<AuditOptions>` sourced from configuration.  
**Rationale**: A hosted service is the simplest approach that doesn't require a separate scheduler or cron container. Purging once per day is sufficient for 90-day retention.  
**Alternatives considered**:  
- *PostgreSQL `pg_cron`* — external dependency; violates the principle of keeping behaviour in application code where it can be tested.  
- *Wolverine scheduled messages* — more complex; WolverineFx has its own persistence requirement for durable scheduling. Rejected as over-engineering.

---

## 6. EF Core Migrations at Startup

**Decision**: `await dbContext.Database.MigrateAsync()` called from `IHostedService` (`DatabaseMigrationService`) that runs before the application begins serving requests, registered as the first hosted service.  
**Rationale**: Startup migration is the simplest approach for a single-instance deployment. The spec explicitly defers CLI migration tooling to a future concern.  
**Risk**: Two instances starting simultaneously could race on migrations. Acceptable for the current Quadlet single-instance deployment topology.

---

## 7. ProxyHost→YARP Translation

Each `ProxyHost` record maps to **one `RouteConfig` + one `ClusterConfig`**:

| ProxyHost field | YARP concept |
|-----------------|-------------|
| `Id` (as string) | `RouteId`, `ClusterId` prefix |
| `DomainNames` | `RouteMatch.Hosts` |
| `DestinationUri` | `ClusterConfig.Destinations["primary"].Address` |
| `IsEnabled` | Route/cluster included only when `true` |
| Implicit | `RouteMatch.Path = "/{**catch-all}"`, `Order = 100` |

A static `ProxyHostYarpTranslator` class performs this mapping.

---

## 8. Test Database Strategy

**Decision**: `Testcontainers.PostgreSql` (via `Testcontainers` NuGet) for integration tests.  
**Rationale**: The constitution requires integration tests to cover each API endpoint. When the repository implementations are PostgreSQL-backed, tests must run against a real PostgreSQL instance. TestContainers spins up a disposable container per test class, giving full isolation without requiring a pre-configured test database. The `TestWebAppFactory` is extended to replace the DI registration with one pointing at the container's connection string.  
**Alternatives considered**:  
- *SQLite in-memory* — EF Core SQLite provider has different migration and array-type behaviour. Would silently miss Npgsql-specific bugs. Rejected.  
- *Pre-configured CI database* — not portable; requires environment setup. Rejected.
