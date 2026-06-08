# Tasks: Database Persistence for Proxy Configuration

**Input**: Design documents from `/specs/004-db-persistence/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Constitution**: Tests MUST be written and confirmed failing before implementation (Red-Green-Refactor).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup (Package and Project Wiring)

**Purpose**: Structural changes that must compile before any implementation begins. All tasks here block Phase 2.

- [x] T001 Add EF Core 10 + Npgsql packages to `src/ProxyManager.Infrastructure/ProxyManager.Infrastructure.csproj` (`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`)
- [x] T002 [P] Add `Testcontainers.PostgreSql` package to `tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj`
- [x] T003 [P] Add `<ProjectReference>` to `ProxyManager.Infrastructure` and `WolverineFx.RabbitMQ` package to `src/ProxyManager/ProxyManager.csproj`
- [x] T004 [P] Create `DatabaseOptions` record in `src/ProxyManager.Infrastructure/Options/DatabaseOptions.cs` (`Section = "Database"`, `ConnectionString` property)
- [x] T005 [P] Create `AuditOptions` record in `src/ProxyManager.API/Options/AuditOptions.cs` (`Section = "Audit"`, `RetentionDays` property defaulting to `90`)

**Checkpoint**: `dotnet build ProxyManager.sln` succeeds before proceeding to Phase 2.

---

## Phase 2: Foundational (EF Core Infrastructure)

**Purpose**: DbContext, entity configurations, repository interfaces, and migration scaffolding. MUST complete before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T006 Create `ProxyManagerDbContext` with `DbSet<ProxyHost>` and `DbSet<AuditLogEntry>`, `ApplyConfigurationsFromAssembly` in `src/ProxyManager.Infrastructure/Data/ProxyManagerDbContext.cs`
- [x] T007 [P] Create `ProxyHostConfiguration : IEntityTypeConfiguration<ProxyHost>` mapping `id`, `domain_names` as `text[]`, owned `DestinationUri` columns (`destination_scheme`, `destination_host`, `destination_port`), owned `ProxyCertificate` columns, `is_enabled`, `created_at`, `updated_at` in `src/ProxyManager.Infrastructure/Data/Configurations/ProxyHostConfiguration.cs`
- [x] T008 [P] Create `AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>` mapping all columns and adding index on `occurred_at` in `src/ProxyManager.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs`
- [x] T009 Create `InfrastructureServiceExtensions` with `AddProxyManagerInfrastructure(IServiceCollection services, DatabaseOptions options)` registering `ProxyManagerDbContext` with Npgsql provider and both repository interfaces in `src/ProxyManager.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`
- [x] T010 Update `IAuditLogRepository` interface: add optional `from`/`to`/`page`/`pageSize` parameters to `GetByProxyHostAsync`, add `PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)` returning `Task<int>` in `src/ProxyManager.Core/AggregatesModel/AuditLogAggregate/IAuditLogRepository.cs`
- [x] T011 Update `InMemoryAuditLogRepository` to implement the updated `IAuditLogRepository` interface (apply `from`/`to` filtering and `PurgeOlderThanAsync` no-op returning count) in `src/ProxyManager.Infrastructure/Repositories/InMemoryAuditLogRepository.cs`
- [x] T012 Scaffold initial EF Core migration using `dotnet ef migrations add InitialCreate --project src/ProxyManager.Infrastructure --startup-project src/ProxyManager.API --output-dir Data/Migrations`

**Checkpoint**: `dotnet build ProxyManager.sln` succeeds and migration files exist in `src/ProxyManager.Infrastructure/Data/Migrations/` before proceeding to user stories.

---

## Phase 3: User Story 1 — Proxy Configuration Survives Restarts (P1) 🎯 MVP

**Goal**: ProxyHost records are persisted to PostgreSQL and loaded back on service restart. Administrators never lose configuration across restarts.

**Independent Test**: Create proxy routes via the API, stop and restart ProxyManager.API, then call `GET /proxyhosts` and verify all routes are present and correct.

### Tests for User Story 1 ⚠️ Write FIRST — must FAIL before implementation

- [x] T013 [P] [US1] Write `PostgresProxyHostRepositoryTests` covering `AddAsync`, `FindAsync`, `GetAllAsync`, `UpdateAsync`, `RemoveAsync`, and duplicate-domain conflict detection using `TestContainers.PostgreSql` in `tests/ProxyManager.API.Tests/Integration/Repositories/PostgresProxyHostRepositoryTests.cs`
- [x] T014 [P] [US1] Write `PostgresAuditLogRepositoryTests` covering `AppendAsync`, `GetByProxyHostAsync` (with/without time filters, pagination), `PurgeOlderThanAsync` using `TestContainers.PostgreSql` in `tests/ProxyManager.API.Tests/Integration/Repositories/PostgresAuditLogRepositoryTests.cs`
- [x] T015 [P] [US1] Update `TestWebAppFactory` to spin up a `PostgreSqlContainer` via `IAsyncLifetime`, register `ProxyManagerDbContext` pointing at the container, and run migrations before tests in `tests/ProxyManager.API.Tests/Helpers/TestWebAppFactory.cs`

### Implementation for User Story 1

- [x] T016 [P] [US1] Implement `PostgresProxyHostRepository : IProxyHostRepository` using `ProxyManagerDbContext` with full async EF Core operations, including conflict detection on `GetAllAsync` in `src/ProxyManager.Infrastructure/Repositories/PostgresProxyHostRepository.cs`
- [x] T017 [P] [US1] Implement `PostgresAuditLogRepository : IAuditLogRepository` using `ProxyManagerDbContext` with time-range filtering on `GetByProxyHostAsync` and bulk delete on `PurgeOlderThanAsync` in `src/ProxyManager.Infrastructure/Repositories/PostgresAuditLogRepository.cs`
- [x] T018 [US1] Create `DatabaseMigrationService : IHostedService` that calls `dbContext.Database.MigrateAsync()` on `StartAsync` in `src/ProxyManager.API/Services/DatabaseMigrationService.cs`
- [x] T019 [US1] Update `ServiceCollectionExtensions.AddProxyManagerServices` to read `DatabaseOptions` from `IOptions<DatabaseOptions>`, call `AddProxyManagerInfrastructure`, and remove `InMemory` repository registrations in `src/ProxyManager.API/Infrastructure/ServiceCollectionExtensions.cs`
- [x] T020 [US1] Register `IOptions<DatabaseOptions>` binding and add `DatabaseMigrationService` as the first `AddHostedService` call in `src/ProxyManager.API/Program.cs`
- [x] T021 [US1] Add `Database:ConnectionString` and `Audit:RetentionDays` entries to `src/ProxyManager.API/appsettings.Development.json` pointing at the local PostgreSQL instance per `quickstart.md`

**Checkpoint**: All `PostgresProxyHostRepositoryTests`, `PostgresAuditLogRepositoryTests`, and existing `ProxyHostEndpointsTests` pass against the PostgreSQL-backed implementation. `GET /proxyhosts` returns persisted routes after a service restart.

---

## Phase 4: User Story 2 — Configuration Changes Take Effect Immediately (P2)

**Goal**: After a ProxyHost create/update/delete via the API, YARP in ProxyManager begins routing accordingly within 2 seconds — no restart required.

**Independent Test**: Create a new ProxyHost via the API, then immediately issue an HTTP request matching that host's domain to ProxyManager (port 80) and verify the proxy forwards it correctly without any restart.

### Tests for User Story 2 ⚠️ Write FIRST — must FAIL before implementation

- [ ] T022 [P] [US2] Write `ProxyHostYarpTranslatorTests` covering: enabled host → one RouteConfig + one ClusterConfig; disabled host → excluded; empty list → empty result; domain names map to RouteMatch.Hosts in `tests/ProxyManager.API.Tests/Unit/Yarp/ProxyHostYarpTranslatorTests.cs`
- [ ] T023 [P] [US2] Write `DatabaseProxyConfigProviderTests` covering: `GetConfig()` returns translated routes from repository; `Reload()` fires the change token; second `GetConfig()` after reload returns updated data; startup loads from DB in `tests/ProxyManager.API.Tests/Unit/Yarp/DatabaseProxyConfigProviderTests.cs`
- [ ] T024 [P] [US2] Write `ProxyHostChangedHandlerTests` verifying handler calls `DatabaseProxyConfigProvider.Reload()` for each of `ProxyHostCreatedEvent`, `ProxyHostUpdatedEvent`, `ProxyHostDeletedEvent` in `tests/ProxyManager.API.Tests/Unit/Handlers/ProxyHostChangedHandlerTests.cs`
- [ ] T025 [P] [US2] Write `ProxyConfigSeedServiceTests` covering: empty DB + file present → seeds ProxyHost records; non-empty DB → skips seeding; missing file → skips seeding in `tests/ProxyManager.API.Tests/Unit/Services/ProxyConfigSeedServiceTests.cs`

### Implementation for User Story 2

- [ ] T026 [P] [US2] Implement static `ProxyHostYarpTranslator` with `Translate(IEnumerable<ProxyHost>) → (IReadOnlyList<RouteConfig>, IReadOnlyList<ClusterConfig>)` in `src/ProxyManager/Yarp/ProxyHostYarpTranslator.cs`
- [ ] T027 [US2] Implement `DatabaseProxyConfigProvider : IProxyConfigProvider` using `IServiceScopeFactory` to create scoped `IProxyHostRepository`, holding a `CancellationTokenSource` for the change token, and exposing a `Reload()` method that re-queries and swaps the active `IProxyConfig` instance in `src/ProxyManager/Yarp/DatabaseProxyConfigProvider.cs`
- [ ] T028 [P] [US2] Implement `ProxyHostChangedHandler` as a Wolverine message handler with three `Handle` overloads (one per event type) that inject `DatabaseProxyConfigProvider` and call `Reload()` in `src/ProxyManager/Handlers/ProxyHostChangedHandler.cs`
- [ ] T029 [P] [US2] Implement `ProxyConfigSeedService : IHostedService` that checks `IProxyHostRepository.GetAllAsync()` on startup; if empty, reads `proxysettings.{Environment}.json` `ReverseProxy` section, translates non-system routes to `ProxyHost` records, and calls `IProxyHostRepository.AddAsync` for each in `src/ProxyManager/Services/ProxyConfigSeedService.cs`
- [ ] T030 [US2] Wire `DatabaseProxyConfigProvider` into YARP via `.LoadFromCustomConfig<DatabaseProxyConfigProvider>()` (or `AddTransientConfigProvider`), register `ProxyHostChangedHandler` consumer on the `proxy-hosts` RabbitMQ exchange, and register `ProxyConfigSeedService` as a hosted service in `src/ProxyManager/Program.cs`
- [ ] T031 [US2] Add `Database:ConnectionString` entry and Wolverine RabbitMQ connection settings to `src/ProxyManager/appsettings.Development.json`

**Checkpoint**: All T022–T025 tests pass. Live routing test passes: create a ProxyHost via API → traffic to ProxyManager on matching domain is forwarded immediately without restart.

---

## Phase 5: User Story 3 — Audit Trail of Configuration Changes (P3)

**Goal**: Every ProxyHost create/update/delete is recorded with actor identity and timestamp, and the history is queryable via the API. Records are automatically purged after the configured retention period.

**Independent Test**: Call `POST /proxyhosts`, `PUT /proxyhosts/{id}`, and `DELETE /proxyhosts/{id}`. Then call `GET /proxyhosts/{id}/audit` and verify three entries appear in chronological order with correct operation names, actor identity, and before/after state snapshots.

### Tests for User Story 3 ⚠️ Write FIRST — must FAIL before implementation

- [ ] T032 [P] [US3] Write `AuditLogEndpointsTests` covering: `GET /proxyhosts/{id}/audit` returns 200 with paged entries; `from`/`to` filters exclude out-of-range entries; `pageSize` > 200 returns 400 Problem Details; 401 without token; 404 for unknown ProxyHost ID in `tests/ProxyManager.API.Tests/Integration/AuditLogEndpointsTests.cs`
- [ ] T033 [P] [US3] Write `AuditRetentionJobTests` verifying: job calls `PurgeOlderThanAsync` with `DateTimeOffset.UtcNow - RetentionDays`; job respects `AuditOptions.RetentionDays` value; purged count is logged in `tests/ProxyManager.API.Tests/Unit/Services/AuditRetentionJobTests.cs`

### Implementation for User Story 3

- [ ] T034 [US3] Extend `ProxyHostDto` with nullable `DateTimeOffset? CreatedAt` and `DateTimeOffset? UpdatedAt` properties; update `GetProxyHostsHandler.MapToDto` to populate them from the entity in `src/ProxyManager.Core/DTOs/ProxyHostDto.cs` and `src/ProxyManager.API/Handlers/GetProxyHostsHandler.cs`
- [ ] T035 [US3] Add `CreatedAt` and `UpdatedAt` fields to the `ProxyHost` aggregate; set `CreatedAt` in `ProxyHost.Create()` and `UpdatedAt` in `Enable()`, `Disable()`, `UpdateDestination()`, `UpdateDomainNames()`, `SetCertificate()` in `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/ProxyHost.cs`
- [ ] T036 [P] [US3] Implement `AuditRetentionJob : IHostedService` that executes a daily timer starting at service startup, calling `IAuditLogRepository.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-options.RetentionDays))` and logging the purged count in `src/ProxyManager.API/Services/AuditRetentionJob.cs`
- [ ] T037 [US3] Implement `AuditLogEndpoints` with `MapAuditLogEndpoints(WebApplication)` registering `GET /proxyhosts/{id}/audit` with pagination query params (`from`, `to`, `page`, `pageSize`), 400 validation for `pageSize` > 200, 404 for unknown ProxyHost, and RFC 9457 Problem Details errors in `src/ProxyManager.API/Endpoints/AuditLogEndpoints.cs`
- [ ] T038 [US3] Register `IOptions<AuditOptions>` binding, `AuditRetentionJob` as a hosted service, and call `app.MapAuditLogEndpoints()` in `src/ProxyManager.API/Program.cs`

**Checkpoint**: All T032–T033 tests pass. End-to-end audit flow verified: three ProxyHost mutations produce three audit entries retrievable via the API, and the retention job purges entries beyond the configured window.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Quality verification, documentation, and coverage checks across all stories.

- [ ] T039 [P] Add XML documentation comments to all new public types and methods in `ProxyManager.Infrastructure` (DbContext, repositories, extensions) per Constitution §I
- [ ] T040 [P] Add XML documentation comments to all new public types in `ProxyManager` (`DatabaseProxyConfigProvider`, `ProxyHostYarpTranslator`, handlers, services) per Constitution §I
- [ ] T041 [P] Add XML documentation comments to all new public types in `ProxyManager.API` (`AuditLogEndpoints`, `AuditRetentionJob`, `AuditOptions`) per Constitution §I
- [ ] T042 Run `dotnet test --collect:"XPlat Code Coverage"` across all test projects and verify new code meets ≥ 80% line coverage threshold per Constitution §II
- [ ] T043 Run `dotnet build ProxyManager.sln` in Release configuration and verify zero warnings
- [ ] T044 Follow `quickstart.md` end-to-end on a clean machine: start PostgreSQL, run both services, create a ProxyHost, verify live routing, query audit log, and verify retention job configuration

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; all tasks [P] within phase
- **Foundational (Phase 2)**: Requires Phase 1 complete — BLOCKS all user stories
- **US1 (Phase 3)**: Requires Phase 2 complete
- **US2 (Phase 4)**: Requires Phase 2 complete; independent of US1 (reads from same DB via repo interface)
- **US3 (Phase 5)**: Requires Phase 2 complete; benefits from US1 (PostgreSQL repos), but audit endpoint can be tested with in-memory fallback
- **Polish (Phase 6)**: Requires all desired user stories complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2. No story dependencies.
- **US2 (P2)**: Can start after Phase 2. Independent of US1 — uses `IProxyHostRepository` interface, not a specific implementation.
- **US3 (P3)**: Can start after Phase 2. T034/T035 extend existing ProxyHost types but do not conflict with US1 or US2 work.

### Critical Sequencing Within Stories

**US1**: T013–T015 (tests, RED) → T016–T017 (repos, GREEN) → T018–T021 (wiring)  
**US2**: T022–T025 (tests, RED) → T026 → T027 (depends on T026) → T028–T029 (parallel) → T030–T031 (wiring)  
**US3**: T032–T033 (tests, RED) → T034–T035 → T036–T037 (parallel) → T038 (wiring)

---

## Parallel Opportunities

### Phase 1 (all in parallel after T001)
```
T001 → [T002, T003, T004, T005] (parallel)
```

### Phase 2
```
T006 → [T007, T008] (parallel) → T009 → T010 → T011 → T012
```

### Phase 3 (US1)
```
[T013, T014, T015] (parallel, RED tests)
→ [T016, T017] (parallel, implementations)
→ T018 → T019 → T020 → T021
```

### Phase 4 (US2)
```
[T022, T023, T024, T025] (parallel, RED tests)
→ T026
→ T027
→ [T028, T029] (parallel)
→ T030 → T031
```

### Phase 5 (US3)
```
[T032, T033] (parallel, RED tests)
→ T034 → T035
→ [T036, T037] (parallel)
→ T038
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T005)
2. Complete Phase 2: Foundational (T006–T012) — CRITICAL
3. Complete Phase 3: US1 (T013–T021)
4. **STOP and VALIDATE**: Routes persist across restarts; all existing endpoint tests still pass
5. Merge and deploy

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → Persistence works; routes survive restarts (MVP)
3. US2 → Live reload; changes take effect within 2 seconds
4. US3 → Audit trail queryable; retention enforced
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

After Phase 2:
- Developer A: US1 (repository implementations + API wiring)
- Developer B: US2 (YARP provider + RabbitMQ consumer)
- Developer C: US3 (audit endpoint + retention job)

---

## Notes

- [P] tasks involve different files — safe to run in parallel within their phase
- Constitution §II: Tests MUST be RED before implementation. Do not skip to GREEN.
- `dotnet build ProxyManager.sln` must pass at each phase checkpoint before proceeding
- Commit after each completed task or logical group
- Stop at any checkpoint to validate the user story independently before starting the next
- The `InMemoryProxyHostRepository` is NOT removed — it remains for use in unit tests (handlers, etc.) via the Fake repositories in `tests/ProxyManager.API.Tests/Unit/Fakes/`
