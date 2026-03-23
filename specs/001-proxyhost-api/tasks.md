---

description: "Task list template for feature implementation"
---

# Tasks: Proxy Host Management API

**Input**: Design documents from `/specs/001-proxyhost-api/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: TDD enforced per Constitution Principle II — test tasks precede implementation tasks
within each story phase. Tests MUST be written and confirmed failing before implementation begins.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story this task belongs to (US1–US4)
- Exact file paths included in all descriptions

## Path Conventions

- Source: `src/ProxyManager.API/`, `src/ProxyManager.Core/`, `src/ProxyManager.Infrastructure/`
- Tests: `tests/ProxyManager.API.Tests/`, `tests/ProxyManager.Core.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Package setup and test project initialization.

- [x] T001 Add `WolverineFx` (5.22.0) and `WolverineFx.RabbitMQ` (5.22.0) NuGet package references to `src/ProxyManager.API/ProxyManager.API.csproj`
- [x] T002 [P] Scaffold `tests/ProxyManager.API.Tests/` xunit project: `dotnet new xunit -n ProxyManager.API.Tests -o tests/ProxyManager.API.Tests` then add references to `Microsoft.AspNetCore.Mvc.Testing` (10.0.x), and project reference to `ProxyManager.API.csproj` (WolverineFx obtained transitively via project reference)
- [x] T003 [P] Scaffold `tests/ProxyManager.Core.Tests/` xunit project: `dotnet new xunit -n ProxyManager.Core.Tests -o tests/ProxyManager.Core.Tests` then add project reference to `ProxyManager.Core.csproj`
- [x] T004 Add both test projects to `ProxyManager.sln`: `dotnet sln add tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj tests/ProxyManager.Core.Tests/ProxyManager.Core.Tests.csproj`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, repositories, DI wiring, and test helpers that ALL user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Core Domain Types

- [x] T005 [P] Create `src/ProxyManager.Core/AggregatesModel/AuditLogAggregate/AuditLogEntry.cs` — immutable `record` with fields: `Guid Id`, `string ActorId`, `AuditOperation Operation` (enum: Created/Updated/Deleted), `Guid ProxyHostId`, `string? PreviousState`, `string? NewState`, `DateTimeOffset OccurredAt`; include static `Create(...)` factory and `AuditOperation` enum in same file
- [x] T006 [P] Create `src/ProxyManager.Core/AggregatesModel/AuditLogAggregate/IAuditLogRepository.cs` — interface with `AppendAsync(AuditLogEntry, CancellationToken)`, `GetByProxyHostAsync(Guid, CancellationToken)`, `GetAllAsync(int page, int pageSize, CancellationToken)` methods
- [x] T007 [P] Create `src/ProxyManager.Core/DTOs/ProxyHostDto.cs` with `ProxyHostDto` record (`Guid Id`, `IReadOnlyList<string> DomainNames`, `string Destination`, `bool IsEnabled`, `ProxyCertificateDto? Certificate`) and `ProxyCertificateDto` record (`string CertificatePath`, `string? KeyPath`) in namespace `West94.ProxyManager.Core.DTOs`
- [x] T008 [P] Create `src/ProxyManager.Core/DTOs/PagedResult.cs` — generic `record PagedResult<T>` with `IReadOnlyList<T> Items`, `int TotalCount`, `int Page`, `int PageSize` in namespace `West94.ProxyManager.Core.DTOs`

### CQRS Message Contracts

- [x] T009 [P] Create `src/ProxyManager.Core/Messages/Queries/GetProxyHostsQuery.cs` — `record GetProxyHostsQuery(int Page = 1, int PageSize = 20)` in namespace `West94.ProxyManager.Core.Messages.Queries`
- [x] T010 [P] Create `src/ProxyManager.Core/Messages/Queries/GetProxyHostByIdQuery.cs` — `record GetProxyHostByIdQuery(Guid Id)` in namespace `West94.ProxyManager.Core.Messages.Queries`
- [x] T011 [P] Create `src/ProxyManager.Core/Messages/Commands/CreateProxyHostCommand.cs` — `record CreateProxyHostCommand(IEnumerable<string> DomainNames, string DestinationUri, string? CertificatePath, string? CertificateKeyPath, string ActorId)` in namespace `West94.ProxyManager.Core.Messages.Commands`
- [x] T012 [P] Create `src/ProxyManager.Core/Messages/Commands/UpdateProxyHostCommand.cs` — `record UpdateProxyHostCommand(Guid Id, IEnumerable<string>? DomainNames, string? DestinationUri, bool? IsEnabled, string? CertificatePath, string? CertificateKeyPath, string ActorId)` in namespace `West94.ProxyManager.Core.Messages.Commands`
- [x] T013 [P] Create `src/ProxyManager.Core/Messages/Commands/DeleteProxyHostCommand.cs` — `record DeleteProxyHostCommand(Guid Id, string ActorId)` in namespace `West94.ProxyManager.Core.Messages.Commands`
- [x] T014 [P] Create integration event records in `src/ProxyManager.Core/Messages/Events/`: `ProxyHostCreatedEvent.cs` (`Guid Id`, `IReadOnlyList<string> DomainNames`, `string Destination`, `bool IsEnabled`, `DateTimeOffset OccurredAt`), `ProxyHostUpdatedEvent.cs` (same fields), `ProxyHostDeletedEvent.cs` (`Guid Id`, `IReadOnlyList<string> DomainNames`, `DateTimeOffset OccurredAt`) — all in namespace `West94.ProxyManager.Core.Messages.Events`

### Infrastructure & DI

- [x] T015 Create `src/ProxyManager.Infrastructure/Repositories/InMemoryAuditLogRepository.cs` implementing `IAuditLogRepository` using `ConcurrentQueue<AuditLogEntry>` for append-only storage; implement `AppendAsync`, `GetByProxyHostAsync`, `GetAllAsync` with pagination (depends on T006)
- [x] T016 Create `src/ProxyManager.API/Infrastructure/ServiceCollectionExtensions.cs` with `AddProxyManagerServices(this IServiceCollection)` extension method that registers `IProxyHostRepository` → `InMemoryProxyHostRepository` (singleton) and `IAuditLogRepository` → `InMemoryAuditLogRepository` (singleton) (depends on T015)
- [x] T017 Configure Wolverine and RabbitMQ in `src/ProxyManager.API/Program.cs`: add `builder.Host.UseWolverine(...)` with `WolverineFx.RabbitMQ` transport declaring durable `proxy-hosts` fanout exchange and routing `ProxyHostCreatedEvent`, `ProxyHostUpdatedEvent`, `ProxyHostDeletedEvent` to it; bind `RabbitMqOptions` via `IOptions<T>` from `appsettings.json`; call `builder.Services.AddProxyManagerServices()`; update endpoint registration to call `app.MapProxyHostEndpoints()` instead of `app.MapRouteEndpoints()` (depends on T016, T014)
- [x] T018 Create scaffold `src/ProxyManager.API/Endpoints/ProxyHostEndpoints.cs` with `MapProxyHostEndpoints(this IEndpointRouteBuilder)` extension method that creates `MapGroup("/proxyhosts").WithTags("ProxyHosts").RequireAuthorization()` group — no endpoints mapped yet, just the group setup (depends on T017)

### Test Helpers

- [x] T019 [P] Create `tests/ProxyManager.API.Tests/Helpers/TestWebAppFactory.cs` (`WebApplicationFactory<Program>` subclass that overrides `ConfigureWebHost` to swap Wolverine transport for in-memory and replace RabbitMQ with no-op) and `tests/ProxyManager.API.Tests/Helpers/TestJwtFactory.cs` (generates self-signed test JWT tokens with configurable `sub` claim for integration tests)

**Checkpoint**: Foundation ready — build MUST succeed (`dotnet build ProxyManager.sln`) before proceeding.

---

## Phase 3: User Story 1 — Query Proxy Hosts (Priority: P1) 🎯 MVP

**Goal**: Authorized clients can list all proxy hosts (paginated) and retrieve a single host by ID.
Unauthenticated requests are denied. 404 returned for unknown IDs.

**Independent Test**: Run `GET /proxyhosts` and `GET /proxyhosts/{id}` with a valid token;
confirm 200 responses. Run without a token; confirm 401.

### Tests for User Story 1 ⚠️ Write first — confirm failing before T024

- [x] T020 [P] [US1] Write failing unit tests for `GetProxyHostsHandler` in `tests/ProxyManager.API.Tests/Unit/Handlers/GetProxyHostsHandlerTests.cs`: covers empty repo returns empty paged result; repo with items returns paginated result sorted by first domain name; page/pageSize parameters respected
- [x] T021 [P] [US1] Write failing unit tests for `GetProxyHostByIdHandler` in `tests/ProxyManager.API.Tests/Unit/Handlers/GetProxyHostByIdHandlerTests.cs`: covers found ID returns populated `ProxyHostDto`; unknown ID returns null
- [x] T022 [P] [US1] Write failing integration tests in `tests/ProxyManager.API.Tests/Integration/ProxyHostEndpointsTests.cs`: `GET /proxyhosts` with valid token → 200 with `PagedResult<ProxyHostDto>`; `GET /proxyhosts/{id}` with valid token and existing ID → 200; `GET /proxyhosts/{id}` with unknown ID → 404 Problem Details; `GET /proxyhosts` without token → 401
- [x] T023 [P] [US1] Write failing unit tests for `ProxyHost` aggregate in `tests/ProxyManager.Core.Tests/Unit/ProxyHostAggregateTests.cs`: covers `Create` with valid/invalid args; `Enable`/`Disable`; `UpdateDestination`; `UpdateDomainNames` with empty list throws

### Implementation for User Story 1

- [x] T024 [US1] Implement `GetProxyHostsHandler` in `src/ProxyManager.API/Handlers/GetProxyHostsHandler.cs` — Wolverine handler for `GetProxyHostsQuery` that calls `IProxyHostRepository.GetAllAsync`, sorts by first domain name, applies pagination, and maps to `PagedResult<ProxyHostDto>` (depends on T020)
- [x] T025 [US1] Implement `GetProxyHostByIdHandler` in `src/ProxyManager.API/Handlers/GetProxyHostByIdHandler.cs` — Wolverine handler for `GetProxyHostByIdQuery` that calls `IProxyHostRepository.FindAsync` and maps to `ProxyHostDto?` (depends on T021)
- [x] T026 [US1] Add `GET /proxyhosts` and `GET /proxyhosts/{id:guid}` endpoint mappings to `src/ProxyManager.API/Endpoints/ProxyHostEndpoints.cs`; dispatch `GetProxyHostsQuery` and `GetProxyHostByIdQuery` via `IMessageBus`; return `TypedResults.Ok(result)` or `TypedResults.Problem(...)` (404) with Problem Details (depends on T024, T025, T022)

**Checkpoint**: `GET /proxyhosts` and `GET /proxyhosts/{id}` functional; all US1 tests passing.

---

## Phase 4: User Story 2 — Create a Proxy Host (Priority: P2)

**Goal**: Authorized clients can create a new proxy host. Duplicate hostnames return 409.
Invalid input returns 400 Problem Details. Successful creation writes an audit log entry and
publishes `ProxyHostCreatedEvent` to RabbitMQ.

**Independent Test**: `POST /proxyhosts` with valid payload → 201 with Location header; re-POST
same hostname → 409; `POST` without required fields → 400.

### Tests for User Story 2 ⚠️ Write first — confirm failing before T029

- [ ] T027 [P] [US2] Write failing unit tests for `CreateProxyHostHandler` in `tests/ProxyManager.API.Tests/Unit/Handlers/CreateProxyHostHandlerTests.cs`: covers successful create returns `ProxyHostDto` with new Id; duplicate domain name throws conflict exception; invalid `destinationUri` throws validation exception; audit log entry is appended with operation `Created` and correct actor; `ProxyHostCreatedEvent` is published via `IMessageContext`
- [ ] T028 [P] [US2] Add failing integration tests to `tests/ProxyManager.API.Tests/Integration/ProxyHostEndpointsTests.cs`: `POST /proxyhosts` with valid body → 201 with Location header and ProxyHostDto body; duplicate hostname → 409 Problem Details; missing `destinationUri` → 400 Problem Details; no token → 401

### Implementation for User Story 2

- [ ] T029 [US2] Implement `CreateProxyHostHandler` in `src/ProxyManager.API/Handlers/CreateProxyHostHandler.cs` — Wolverine handler for `CreateProxyHostCommand` that: validates at least one domain name and a valid http/https `destinationUri`; checks for hostname conflicts via `IProxyHostRepository.GetAllAsync`; calls `ProxyHost.Create(...)`; calls `IProxyHostRepository.AddAsync`; appends `AuditLogEntry` (operation `Created`); publishes `ProxyHostCreatedEvent` via `IMessageContext.PublishAsync`; returns `ProxyHostDto` (depends on T027)
- [ ] T030 [US2] Add `POST /proxyhosts` endpoint to `src/ProxyManager.API/Endpoints/ProxyHostEndpoints.cs`; extract actor `sub` claim from `HttpContext.User`; build `CreateProxyHostCommand`; dispatch via `IMessageBus.InvokeAsync<ProxyHostDto>`; return `TypedResults.Created($"/proxyhosts/{dto.Id}", dto)` or Problem Details (400/409) (depends on T029, T028)

**Checkpoint**: `POST /proxyhosts` functional; all US2 tests passing.

---

## Phase 5: User Story 3 — Update a Proxy Host (Priority: P3)

**Goal**: Authorized clients can update any field of an existing proxy host. Non-existent ID
returns 404. Invalid field values return 400. Successful update writes audit log entry and
publishes `ProxyHostUpdatedEvent`.

**Independent Test**: Create a proxy host, `PUT /proxyhosts/{id}` with `isEnabled: false` →
200 with updated state; re-GET the host → `isEnabled` is `false`.

### Tests for User Story 3 ⚠️ Write first — confirm failing before T033

- [ ] T031 [P] [US3] Write failing unit tests for `UpdateProxyHostHandler` in `tests/ProxyManager.API.Tests/Unit/Handlers/UpdateProxyHostHandlerTests.cs`: covers partial update (only `isEnabled` provided) leaves other fields unchanged; unknown Id throws not-found exception; invalid `destinationUri` throws validation exception; audit log entry appended with operation `Updated`, `PreviousState` snapshot, and `NewState` snapshot; `ProxyHostUpdatedEvent` is published
- [ ] T032 [P] [US3] Add failing integration tests to `tests/ProxyManager.API.Tests/Integration/ProxyHostEndpointsTests.cs`: `PUT /proxyhosts/{id}` with valid partial update → 200; unknown id → 404 Problem Details; malformed `destinationUri` → 400 Problem Details; no token → 401

### Implementation for User Story 3

- [ ] T033 [US3] Implement `UpdateProxyHostHandler` in `src/ProxyManager.API/Handlers/UpdateProxyHostHandler.cs` — Wolverine handler for `UpdateProxyHostCommand` that: loads host via `FindAsync` (404 if missing); captures previous state as JSON snapshot; applies only non-null fields (`DomainNames`, `DestinationUri`, `IsEnabled`, `CertificatePath`); calls `IProxyHostRepository.UpdateAsync`; appends `AuditLogEntry` (operation `Updated` with both snapshots); publishes `ProxyHostUpdatedEvent`; returns updated `ProxyHostDto` (depends on T031)
- [ ] T034 [US3] Add `PUT /proxyhosts/{id:guid}` endpoint to `src/ProxyManager.API/Endpoints/ProxyHostEndpoints.cs`; build `UpdateProxyHostCommand` with actor from claims; dispatch via `IMessageBus`; return `TypedResults.Ok(dto)` or Problem Details (400/404) (depends on T033, T032)

**Checkpoint**: `PUT /proxyhosts/{id}` functional; all US3 tests passing.

---

## Phase 6: User Story 4 — Delete a Proxy Host (Priority: P4)

**Goal**: Authorized clients can permanently delete a proxy host. Non-existent ID returns 404.
Successful deletion writes audit log entry and publishes `ProxyHostDeletedEvent`.

**Independent Test**: Create a proxy host, `DELETE /proxyhosts/{id}` → 204; `GET /proxyhosts/{id}` → 404.

### Tests for User Story 4 ⚠️ Write first — confirm failing before T037

- [ ] T035 [P] [US4] Write failing unit tests for `DeleteProxyHostHandler` in `tests/ProxyManager.API.Tests/Unit/Handlers/DeleteProxyHostHandlerTests.cs`: covers successful delete calls `RemoveAsync`; unknown Id throws not-found exception; audit log entry appended with operation `Deleted` and `PreviousState` snapshot; `ProxyHostDeletedEvent` published with correct domain names
- [ ] T036 [P] [US4] Add failing integration tests to `tests/ProxyManager.API.Tests/Integration/ProxyHostEndpointsTests.cs`: `DELETE /proxyhosts/{id}` with existing id → 204; second delete of same id → 404 Problem Details; no token → 401

### Implementation for User Story 4

- [ ] T037 [US4] Implement `DeleteProxyHostHandler` in `src/ProxyManager.API/Handlers/DeleteProxyHostHandler.cs` — Wolverine handler for `DeleteProxyHostCommand` that: loads host via `FindAsync` (404 if missing); captures state as JSON snapshot for audit; calls `IProxyHostRepository.RemoveAsync`; appends `AuditLogEntry` (operation `Deleted` with `PreviousState`, null `NewState`); publishes `ProxyHostDeletedEvent` (depends on T035)
- [ ] T038 [US4] Add `DELETE /proxyhosts/{id:guid}` endpoint to `src/ProxyManager.API/Endpoints/ProxyHostEndpoints.cs`; build `DeleteProxyHostCommand` with actor from claims; dispatch via `IMessageBus`; return `TypedResults.NoContent()` or Problem Details (404) (depends on T037, T036)

**Checkpoint**: All four user stories functional and independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, coverage gate, and end-to-end validation.

- [ ] T039 [P] Delete `src/ProxyManager.API/Endpoints/RouteEndpoints.cs` (replaced by `ProxyHostEndpoints.cs`); confirm `Program.cs` no longer references `MapRouteEndpoints()`
- [ ] T040 [P] Write unit tests for `AuditLogEntry` in `tests/ProxyManager.Core.Tests/Unit/AuditLogEntryTests.cs`: factory method sets Id, OccurredAt in UTC, and validates ActorId not empty; `PreviousState` null for `Created`; `NewState` null for `Deleted`
- [ ] T041 Run `dotnet test ProxyManager.sln --collect:"XPlat Code Coverage"` and verify ≥80% line coverage for all new code in `ProxyManager.API/Handlers/`, `ProxyManager.Core/Messages/`, `ProxyManager.Core/AggregatesModel/AuditLogAggregate/`, and `ProxyManager.Infrastructure/Repositories/`
- [ ] T042 Validate quickstart.md manually: start RabbitMQ, run API, execute all five curl commands from `specs/001-proxyhost-api/quickstart.md`, confirm RabbitMQ management UI shows published events on the `proxy-hosts` exchange

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Stories (Phases 3–6)**: All depend on Phase 2 completion; stories can proceed in parallel by priority or sequentially P1 → P2 → P3 → P4
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — no story dependencies
- **US2 (P2)**: Can start after Phase 2 — no story dependencies (uses same test file as US1 integration tests, so add to existing file)
- **US3 (P3)**: Can start after Phase 2 — no story dependencies
- **US4 (P4)**: Can start after Phase 2 — no story dependencies

### Within Each User Story

- Tests (T020–T023, T027–T028, T031–T032, T035–T036) MUST be written and **confirmed failing** before implementation
- Models/records before services/handlers
- Handlers before endpoint mappings

### Parallel Opportunities

- T002 and T003 (test project creation) run in parallel
- T005–T014 (all Core types and message records) run in parallel
- T020–T023 (US1 test writing) run in parallel
- T027–T028 (US2 test writing) run in parallel
- T031–T032 (US3 test writing) run in parallel
- T035–T036 (US4 test writing) run in parallel
- T039 and T040 (polish tasks) run in parallel
- All four user story phases can be parallelized across developers after Phase 2

---

## Parallel Example: User Story 1

```bash
# Launch all US1 test tasks together:
Task: "T020 — GetProxyHostsHandler unit tests"
Task: "T021 — GetProxyHostByIdHandler unit tests"
Task: "T022 — Integration tests for GET endpoints"
Task: "T023 — ProxyHost aggregate unit tests"

# Confirm all fail, then launch implementation:
Task: "T024 — GetProxyHostsHandler implementation"
Task: "T025 — GetProxyHostByIdHandler implementation"
# T026 depends on T024 and T025 — run after both complete
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks all stories)
3. Write and confirm failing tests T020–T023
4. Complete Phase 3: User Story 1 (T024–T026)
5. **STOP and VALIDATE**: `dotnet test` all US1 tests pass; curl `GET /proxyhosts` returns 200
6. Demo the read-only routing table view

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready
2. Phase 3 (US1) → Read-only query API (MVP!)
3. Phase 4 (US2) → Create capability; first RabbitMQ event published
4. Phase 5 (US3) → Update capability + audit trail
5. Phase 6 (US4) → Full CRUD complete
6. Phase 7 → Coverage gate and cleanup

---

## Notes

- `[P]` = different files, no blocking dependencies — safe to run in parallel
- `[Story]` label maps task to user story for traceability
- Integration tests share one file (`ProxyHostEndpointsTests.cs`) — add test methods incrementally per story
- `RouteEndpoints.cs` stub is replaced (not modified) — delete in T039
- In-memory repos do not survive restart — document this limitation in PR description
- Wolverine transport MUST be swapped to in-memory (`TestWebAppFactory`) for integration tests to avoid requiring a live RabbitMQ instance in CI
