# Implementation Plan: Database Persistence for Proxy Configuration

**Branch**: `004-db-persistence` | **Date**: 2026-06-07 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/004-db-persistence/spec.md`

## Summary

Replace the two in-memory repositories (`InMemoryProxyHostRepository`, `InMemoryAuditLogRepository`) with EF Core + PostgreSQL implementations, wire a custom `IProxyConfigProvider` into ProxyManager that loads ProxyHost records from the database and reloads live YARP routing when RabbitMQ config-change events arrive, add an audit log query endpoint, and enforce a configurable 90-day retention policy via a background job.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 + ASP.NET Core  
**Primary Dependencies**:
- EF Core 10 (`Microsoft.EntityFrameworkCore`) + `Npgsql.EntityFrameworkCore.PostgreSQL` 10 — added to `ProxyManager.Infrastructure`
- `Testcontainers.PostgreSql` — added to `ProxyManager.API.Tests` for integration tests
- `WolverineFx` + `WolverineFx.RabbitMQ` — added to `ProxyManager` (proxy service) for RabbitMQ event consumption
- `dotnet-ef` global tool for migration management

**Storage**: PostgreSQL 16 — `proxy_hosts` and `audit_log_entries` tables; see [data-model.md](data-model.md)  
**Testing**: xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`  
**Target Platform**: Linux (Podman Quadlet container)  
**Performance Goals**:
- Startup with 1,000 ProxyHost records: ≤ 10 seconds (SC-003)
- Config change reflected in live routing: ≤ 2 seconds (SC-001a)
- Management API p95 response time: ≤ 200 ms (Constitution §IV)
- YARP routing overhead: < 5 ms p99 added (Constitution §IV)

**Constraints**:
- All configuration via `IOptions<T>` — raw `IConfiguration` injection forbidden (Constitution §I)
- File-scoped namespaces and primary constructors throughout
- EF Core migrations applied at startup; CLI path not blocked architecturally
- ProxyManager's system routes (`apiRoute`, `ui-api-route`, `ui-route`) remain in `proxysettings.{Environment}.json` and are unaffected

**Scale/Scope**: Single-instance Quadlet deployment; up to 1,000 ProxyHost records

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked post-design below.*

| Gate | Principle | Status |
|------|-----------|--------|
| Tests written and confirmed failing before implementation begins | II. Testing Standards | ✅ All tasks order tests before implementation |
| API error responses use RFC 9457 Problem Details | III. User Experience Consistency | ✅ Existing `ProxyHostEndpoints` already maps domain exceptions to Problem Details; new audit endpoint follows same pattern |
| Performance goals and latency budgets documented in Technical Context | IV. Performance Requirements | ✅ Documented above |
| No raw `IConfiguration` injection; `IOptions<T>` used throughout | I. Code Quality | ✅ `DatabaseOptions` and `AuditOptions` use `IOptions<T>` |

**Post-design re-check**: No constitution violations introduced. The DatabaseProxyConfigProvider uses constructor-injected `IServiceScopeFactory` (not raw `IProxyHostRepository`) to create scoped EF Core contexts. All new services use primary constructors and file-scoped namespaces.

## Project Structure

### Documentation (this feature)

```text
specs/004-db-persistence/
├── plan.md              ← this file
├── research.md          ← Phase 0
├── data-model.md        ← Phase 1
├── quickstart.md        ← Phase 1
├── contracts/
│   └── audit-log-api.md ← Phase 1
└── tasks.md             ← Phase 2 (/speckit.tasks)
```

### Source Code Changes

```text
src/
├── ProxyManager/
│   ├── Yarp/
│   │   ├── DatabaseProxyConfigProvider.cs    [NEW]  — IProxyConfigProvider backed by DB
│   │   └── ProxyHostYarpTranslator.cs        [NEW]  — ProxyHost → RouteConfig/ClusterConfig
│   ├── Services/
│   │   └── ProxyConfigSeedService.cs         [NEW]  — Seeds DB from proxysettings.json on first run
│   ├── Handlers/
│   │   └── ProxyHostChangedHandler.cs        [NEW]  — Wolverine RabbitMQ consumer → calls Reload()
│   ├── Program.cs                            [MODIFIED] — add DB provider, Wolverine RabbitMQ
│   └── ProxyManager.csproj                   [MODIFIED] — add Infrastructure ref + WolverineFx.RabbitMQ
│
├── ProxyManager.API/
│   ├── Endpoints/
│   │   └── AuditLogEndpoints.cs              [NEW]  — GET /proxyhosts/{id}/audit
│   ├── Services/
│   │   └── AuditRetentionJob.cs              [NEW]  — IHostedService, daily purge
│   ├── Options/
│   │   └── AuditOptions.cs                   [NEW]  — RetentionDays setting
│   ├── Infrastructure/
│   │   └── ServiceCollectionExtensions.cs    [MODIFIED] — swap in-memory → Postgres repos
│   └── Program.cs                            [MODIFIED] — register AuditOptions, AuditRetentionJob, run migrations
│
├── ProxyManager.Core/
│   └── AggregatesModel/AuditLogAggregate/
│       └── IAuditLogRepository.cs            [MODIFIED] — add time-range + PurgeOlderThanAsync
│
└── ProxyManager.Infrastructure/
    ├── Data/
    │   ├── ProxyManagerDbContext.cs           [NEW]
    │   ├── Configurations/
    │   │   ├── ProxyHostConfiguration.cs     [NEW]
    │   │   └── AuditLogEntryConfiguration.cs [NEW]
    │   └── Migrations/                        [NEW — EF generated]
    ├── Repositories/
    │   ├── PostgresProxyHostRepository.cs    [NEW]
    │   └── PostgresAuditLogRepository.cs     [NEW]
    ├── Extensions/
    │   └── InfrastructureServiceExtensions.cs [NEW]
    └── ProxyManager.Infrastructure.csproj    [MODIFIED] — add EF Core + Npgsql packages

tests/
├── ProxyManager.API.Tests/
│   ├── Helpers/
│   │   └── TestWebAppFactory.cs              [MODIFIED] — add TestContainers PostgreSQL
│   └── Integration/
│       └── AuditLogEndpointsTests.cs         [NEW]
└── ProxyManager.Core.Tests/
    └── Unit/
        └── InMemoryAuditLogRepositoryTests.cs [MODIFIED] — cover new interface methods
```

**Structure Decision**: Single multi-project .NET solution. Infrastructure project is the only DB-aware layer; both `ProxyManager` (proxy) and `ProxyManager.API` (management) reference it. This aligns with the existing solution layout and keeps domain and application code DB-agnostic.

## Implementation Phases

### Phase A — Infrastructure Foundation

Goal: EF Core DbContext, entity configurations, migrations, PostgreSQL repository implementations, updated `IAuditLogRepository` interface.

**Deliverables**:
1. `ProxyManager.Infrastructure.csproj` updated with EF Core 10 + Npgsql packages
2. `ProxyManagerDbContext` with `ProxyHosts` and `AuditLogEntries` `DbSet`s
3. `ProxyHostConfiguration` — maps `text[]` for DomainNames, owned value objects for `DestinationUri` and `ProxyCertificate`
4. `AuditLogEntryConfiguration` — maps all columns, `occurred_at` index
5. `PostgresProxyHostRepository` — implements `IProxyHostRepository` using EF Core
6. `PostgresAuditLogRepository` — implements updated `IAuditLogRepository`
7. `IAuditLogRepository` updated (time-range + purge parameters)
8. `InMemoryAuditLogRepository` updated to match interface
9. `InfrastructureServiceExtensions` — `AddProxyManagerInfrastructure(IServiceCollection, DatabaseOptions)` registers DbContext + both repositories
10. Initial EF Core migration created

**Tests** (RED before implementation):
- `PostgresProxyHostRepositoryTests` (integration, TestContainers): CRUD, conflict detection, GetAll
- `PostgresAuditLogRepositoryTests` (integration, TestContainers): Append, GetByProxyHost with time filters, PurgeOlderThan
- `InMemoryAuditLogRepositoryTests`: cover new method signatures

---

### Phase B — API Wiring and Audit Endpoint

Goal: ProxyManager.API uses PostgreSQL repositories; startup migrations run; audit endpoint live.

**Deliverables**:
1. `DatabaseOptions` and `AuditOptions` added; registered via `IOptions<T>`
2. `ServiceCollectionExtensions.AddProxyManagerServices` updated to call `AddProxyManagerInfrastructure`; in-memory registrations removed
3. `DatabaseMigrationService` (`IHostedService`) runs `MigrateAsync()` at startup
4. `AuditRetentionJob` (`IHostedService`) purges entries older than `AuditOptions.RetentionDays` daily
5. `AuditLogEndpoints` — `GET /proxyhosts/{id}/audit` with pagination and time-range filters, RFC 9457 errors
6. `ProxyHostDto` extended with nullable `CreatedAt` / `UpdatedAt` fields
7. `TestWebAppFactory` extended: PostgreSQL TestContainer replaces in-memory repo; container shared per test class via `IAsyncLifetime`
8. All existing integration tests continue to pass against the new PostgreSQL-backed implementation

**Tests** (RED before implementation):
- `AuditLogEndpointsTests`: GET with valid token, time filters, pagination, 401, 404
- Verify existing `ProxyHostEndpointsTests` pass (regression)
- Retention job unit test: verify purge is called with correct cutoff date

---

### Phase C — Live Reload in ProxyManager

Goal: ProxyManager loads routes from DB at startup, reloads live when RabbitMQ events arrive.

**Deliverables**:
1. `ProxyManager.csproj` updated: add `ProxyManager.Infrastructure` reference + `WolverineFx.RabbitMQ`
2. `ProxyHostYarpTranslator` — pure static translation of `ProxyHost` → `RouteConfig` + `ClusterConfig`
3. `DatabaseProxyConfigProvider` — implements `IProxyConfigProvider`; uses `IServiceScopeFactory` to create scoped `IProxyHostRepository`; exposes `Reload()` method; fires `CancellationChangeToken` on reload
4. `ProxyHostChangedHandler` — Wolverine handler for `ProxyHostCreatedEvent`, `ProxyHostUpdatedEvent`, `ProxyHostDeletedEvent`; calls `DatabaseProxyConfigProvider.Reload()`
5. `ProxyConfigSeedService` (`IHostedService`) — checks if DB is empty at startup; if so, reads `proxysettings.{Environment}.json` ReverseProxy section, translates non-system routes to ProxyHost records, seeds DB
6. `ProxyManager/Program.cs` updated: register `DatabaseProxyConfigProvider`, Wolverine RabbitMQ consumer for `proxy-hosts` exchange, `ProxyConfigSeedService`

**Tests** (RED before implementation):
- `ProxyHostYarpTranslatorTests` (unit): enabled host translates to route + cluster; disabled host excluded; empty list returns empty config
- `DatabaseProxyConfigProviderTests` (unit, fake repository): GetConfig returns translated routes; Reload fires change token; startup loads from DB
- `ProxyHostChangedHandlerTests` (unit, mock provider): handler calls Reload on all three event types
- `ProxyConfigSeedServiceTests` (unit): empty DB + file present → seeds; non-empty DB → skips; no file → skips

---

## Complexity Tracking

No constitution violations.

| Decision | Why Needed |
|----------|-----------|
| ProxyManager references Infrastructure | DatabaseProxyConfigProvider needs `IProxyHostRepository` to load routes from DB at startup and on reload. No alternative exists without duplicating data access logic or introducing an inter-process HTTP call. |
| WolverineFx.RabbitMQ added to ProxyManager | ProxyManager must react to config-change events published by ProxyManager.API within ≤ 2 seconds. RabbitMQ subscription is the only mechanism consistent with the existing architecture; polling was rejected (see research.md §2). |
| IServiceScopeFactory in DatabaseProxyConfigProvider | `IProxyConfigProvider` is a singleton; EF Core `DbContext` is scoped. Scope factory is the correct pattern for consuming scoped services from singletons in ASP.NET Core. |
