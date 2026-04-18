# Tasks: User Management API

**Input**: Design documents from `/specs/003-user-mgmt-api/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓  
**Tests**: Included — Constitution Principle II mandates Red-Green-Refactor; tests MUST be written and confirmed failing before each implementation task.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)
- **Red**: Write test first; confirm it **fails** before writing implementation

## Path Conventions

All paths are relative to the repository root.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the authorization policy, DI registration, and test helpers that every user story depends on. No user story implementation can start until these are in place.

- [x] T001 [P] Add `"UserAdmin"` authorization policy (`RequireClaim("pm_role", "Admin")`) to `AddProxyManagerServices()` in `src/ProxyManager.API/Infrastructure/ServiceCollectionExtensions.cs`
- [x] T002 [P] Extend `TestJwtFactory` with `CreateToken(string sub, string pmRole)` overload that adds a `pm_role` claim in `tests/ProxyManager.API.Tests/Helpers/TestJwtFactory.cs`

**Checkpoint**: Authorization policy registered; test helper supports role-differentiated tokens.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, interfaces, DTOs, messages, in-memory repositories, and fakes that ALL user stories depend on. MUST be complete before any user story phase begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Enumerations & Exceptions

- [ ] T003 [P] Create `UserAccessLevel` enum (`Admin`, `ReadOnly`) in `src/ProxyManager.Core/AggregatesModel/UserAggregate/UserAccessLevel.cs`
- [ ] T004 [P] Create `UserStatus` enum (`Active`, `Deactivated`) in `src/ProxyManager.Core/AggregatesModel/UserAggregate/UserStatus.cs`
- [ ] T005 [P] Create `UserOperation` enum (`Created`, `Updated`, `Deactivated`, `Reactivated`) in `src/ProxyManager.Core/AggregatesModel/UserAggregate/UserOperation.cs`
- [ ] T006 [P] Create `UserNotFoundException` (message: "No active user with sub '{sub}' was found.") in `src/ProxyManager.Core/Exceptions/UserNotFoundException.cs`
- [ ] T007 [P] Create `UserConflictException` (message: "A user with sub '{sub}' is already active.") in `src/ProxyManager.Core/Exceptions/UserConflictException.cs`
- [ ] T008 [P] Create `UserValidationException` (message: validation detail) in `src/ProxyManager.Core/Exceptions/UserValidationException.cs`

### Aggregate Unit Tests — Red Phase ⚠️

> **Write these tests FIRST. Run `dotnet test` and confirm they FAIL before T010.**

- [ ] T009 Write `AuthorizedUserAggregateTests` covering: Create sets all fields correctly; Deactivate sets `Status=Deactivated` and `DeactivatedAt`; Reactivate clears `DeactivatedAt` and sets `Status=Active`; `ProfileImageUrl` validation rejects non-absolute URLs; `Create` throws `UserValidationException` on blank required fields — in `tests/ProxyManager.Core.Tests/Unit/AuthorizedUserAggregateTests.cs`

### Aggregate & Audit Entry Implementation — Green Phase

- [ ] T010 Implement `AuthorizedUser` aggregate root with fields: `Sub`, `DisplayName`, `Nickname`, `Email`, `ProfileImageUrl?`, `AccessLevel`, `Status`, `CreatedAt`, `LastModifiedAt`, `DeactivatedAt?`; static `Create(...)` factory; `Deactivate()`, `Reactivate(accessLevel)`, `UpdateAccessLevel(accessLevel)` methods; `ProfileImageUrl` validated as absolute `http`/`https` URI via `Uri.TryCreate`; XML doc on all public members — in `src/ProxyManager.Core/AggregatesModel/UserAggregate/AuthorizedUser.cs` (depends on T003–T008, T009)
- [ ] T011 [P] Implement `UserAuditEntry` sealed record with static `Create(subjectSub, operation, previousAccessLevel?, newAccessLevel?, actorSub)` factory; XML doc — in `src/ProxyManager.Core/AggregatesModel/UserAggregate/UserAuditEntry.cs` (depends on T003, T005)

### Repository Interfaces

- [ ] T012 [P] Create `IAuthorizedUserRepository` with `GetBySubAsync`, `GetAllAsync(includeDeactivated, page, pageSize)`, `AddAsync`, `UpdateAsync` — XML doc on all members — in `src/ProxyManager.Core/AggregatesModel/UserAggregate/IAuthorizedUserRepository.cs` (depends on T010)
- [ ] T013 [P] Create `IUserAuditRepository` with `AppendAsync`, `QueryAsync(subFilter?, from?, to?, page, pageSize)` — XML doc on all members — in `src/ProxyManager.Core/AggregatesModel/UserAggregate/IUserAuditRepository.cs` (depends on T011)

### DTOs

- [ ] T014 [P] Create `AuthorizedUserDto` record (`Sub`, `DisplayName`, `Nickname`, `Email`, `ProfileImageUrl?`, `AccessLevel`, `Status`, `CreatedAt`, `LastModifiedAt`, `DeactivatedAt?`) — XML doc — in `src/ProxyManager.Core/DTOs/AuthorizedUserDto.cs`
- [ ] T015 [P] Create `UserAuditEntryDto` record (`Id`, `SubjectSub`, `Operation`, `PreviousAccessLevel?`, `NewAccessLevel?`, `ActorSub`, `OccurredAt`) — XML doc — in `src/ProxyManager.Core/DTOs/UserAuditEntryDto.cs`

### Message Types

- [ ] T016 [P] Create `CreateAuthorizedUserCommand` record (`Sub`, `DisplayName`, `Nickname`, `Email`, `ProfileImageUrl?`, `AccessLevel`, `ActorSub`) in `src/ProxyManager.Core/Messages/Commands/CreateAuthorizedUserCommand.cs`
- [ ] T017 [P] Create `UpdateUserAccessLevelCommand` record (`Sub`, `NewAccessLevel`, `ActorSub`) in `src/ProxyManager.Core/Messages/Commands/UpdateUserAccessLevelCommand.cs`
- [ ] T018 [P] Create `DeactivateUserCommand` record (`Sub`, `ActorSub`) in `src/ProxyManager.Core/Messages/Commands/DeactivateUserCommand.cs`
- [ ] T019 [P] Create `GetAuthorizedUsersQuery` record (`IncludeDeactivated`, `Page`, `PageSize`) in `src/ProxyManager.Core/Messages/Queries/GetAuthorizedUsersQuery.cs`
- [ ] T020 [P] Create `GetAuthorizedUserBySubQuery` record (`Sub`) in `src/ProxyManager.Core/Messages/Queries/GetAuthorizedUserBySubQuery.cs`
- [ ] T021 [P] Create `GetUserAuditLogQuery` record (`SubFilter?`, `From?`, `To?`, `Page`, `PageSize`) in `src/ProxyManager.Core/Messages/Queries/GetUserAuditLogQuery.cs`
- [ ] T022 [P] Create `UserCreatedEvent`, `UserAccessLevelUpdatedEvent`, `UserDeactivatedEvent`, `UserReactivatedEvent` records in `src/ProxyManager.Core/Messages/Events/` (one file each)

### In-Memory Repository Implementations

- [ ] T023 Implement `InMemoryAuthorizedUserRepository` using `ConcurrentDictionary<string, AuthorizedUser>` keyed by `Sub`; `GetAllAsync` filters by `Status` when `includeDeactivated=false`; returns `PagedResult<AuthorizedUser>` — in `src/ProxyManager.Infrastructure/Repositories/InMemoryAuthorizedUserRepository.cs` (depends on T010, T012)
- [ ] T024 Implement `InMemoryUserAuditRepository` using `ConcurrentQueue<UserAuditEntry>`; `QueryAsync` filters by `SubjectSub`, `OccurredAt` range, and paginates — in `src/ProxyManager.Infrastructure/Repositories/InMemoryUserAuditRepository.cs` (depends on T011, T013)

### Test Fakes

- [ ] T025 [P] Implement `FakeAuthorizedUserRepository` (in-memory, implements `IAuthorizedUserRepository`) in `tests/ProxyManager.API.Tests/Unit/Fakes/FakeAuthorizedUserRepository.cs` (depends on T012)
- [ ] T026 [P] Implement `FakeUserAuditRepository` (in-memory, implements `IUserAuditRepository`; exposes `Entries` for assertion) in `tests/ProxyManager.API.Tests/Unit/Fakes/FakeUserAuditRepository.cs` (depends on T013)

### DI Registration

- [ ] T027 Register `IAuthorizedUserRepository → InMemoryAuthorizedUserRepository` and `IUserAuditRepository → InMemoryUserAuditRepository` as singletons in `AddProxyManagerServices()` in `src/ProxyManager.API/Infrastructure/ServiceCollectionExtensions.cs` (depends on T023, T024)

**Checkpoint**: Domain model complete; repositories and fakes ready; DI wired; all tests in T009 now PASS.

---

## Phase 3: User Story 1 — Query Authorized Users (Priority: P1) 🎯 MVP

**Goal**: Authenticated clients can list all active authorized users (paginated) and retrieve a single user by `sub`. Unauthenticated requests are rejected.

**Independent Test**: Seed one user via `FakeAuthorizedUserRepository`, call `GET /v1/users` with a valid token, assert 200 with the user in the result. Call without a token, assert 401.

### Handler Tests — Red Phase ⚠️

> **Write these tests FIRST. Confirm they FAIL before T030–T031.**

- [ ] T028 [P] [US1] Write `GetAuthorizedUsersHandlerTests`: returns `PagedResult<AuthorizedUserDto>` for active users; excludes deactivated by default; includes deactivated when flag set; maps all DTO fields including `Nickname` and `ProfileImageUrl` — in `tests/ProxyManager.API.Tests/Unit/Handlers/GetAuthorizedUsersHandlerTests.cs`
- [ ] T029 [P] [US1] Write `GetAuthorizedUserBySubHandlerTests`: returns `AuthorizedUserDto` for known `sub`; returns `null` for unknown `sub` — in `tests/ProxyManager.API.Tests/Unit/Handlers/GetAuthorizedUserBySubHandlerTests.cs`

### Handler Implementation — Green Phase

- [ ] T030 [P] [US1] Implement `GetAuthorizedUsersHandler` handling `GetAuthorizedUsersQuery` → `PagedResult<AuthorizedUserDto>` — in `src/ProxyManager.API/Handlers/GetAuthorizedUsersHandler.cs` (depends on T028)
- [ ] T031 [P] [US1] Implement `GetAuthorizedUserBySubHandler` handling `GetAuthorizedUserBySubQuery` → `AuthorizedUserDto?` — in `src/ProxyManager.API/Handlers/GetAuthorizedUserBySubHandler.cs` (depends on T029)

### Endpoint & Integration

- [ ] T032 [US1] Create `UserEndpoints.cs`: declare `MapGroup("/v1/users")` with `RequireAuthorization()`; add `GET /` (list) and `GET /audit` (literal — declared BEFORE `GET /{sub}` to prevent route ambiguity) and `GET /{sub}` endpoints; wire to message bus; return `TypedResults.Problem(...)` for 404; XML doc on request/response records — in `src/ProxyManager.API/Endpoints/UserEndpoints.cs` (depends on T030, T031)
- [ ] T033 [US1] Register `app.MapUserEndpoints()` in `src/ProxyManager.API/Program.cs`
- [ ] T034 [US1] Write integration tests for `GET /v1/users` (200 with valid token, 401 without) and `GET /v1/users/{sub}` (200 found, 404 not found, 401 without token) — in `tests/ProxyManager.API.Tests/Integration/UserEndpointsTests.cs` (depends on T032, T002)

**Checkpoint**: `GET /v1/users` and `GET /v1/users/{sub}` fully functional and tested independently. MVP deliverable.

---

## Phase 4: User Story 2 — Add or Update Authorized Users (Priority: P2)

**Goal**: Admin clients can create new users (or reactivate deactivated ones) and update an existing user's access level. Every mutation writes an audit entry.

**Independent Test**: POST a new user with an Admin token; assert 201 with `Location` header. POST the same user again while active; assert 409. POST a deactivated user; assert 200 with `X-User-Reactivated: true`. Use a ReadOnly token for any write; assert 403.

### Handler Tests — Red Phase ⚠️

> **Write these tests FIRST. Confirm they FAIL before T037–T038.**

- [ ] T035 [P] [US2] Write `CreateAuthorizedUserHandlerTests` covering: new user → returns `AuthorizedUserDto` and appends `UserAuditEntry(Created)`; deactivated user → reactivates and appends `UserAuditEntry(Reactivated)`; active user → throws `UserConflictException`; blank `Sub` → throws `UserValidationException`; invalid `ProfileImageUrl` → throws `UserValidationException`; audit failure → propagates exception — in `tests/ProxyManager.API.Tests/Unit/Handlers/CreateAuthorizedUserHandlerTests.cs`
- [ ] T036 [P] [US2] Write `UpdateUserAccessLevelHandlerTests` covering: known active user → updates `AccessLevel`, updates `LastModifiedAt`, appends `UserAuditEntry(Updated)` with previous and new level; unknown sub → throws `UserNotFoundException`; same level → still writes audit entry — in `tests/ProxyManager.API.Tests/Unit/Handlers/UpdateUserAccessLevelHandlerTests.cs`

### Handler Implementation — Green Phase

- [ ] T037 [P] [US2] Implement `CreateAuthorizedUserHandler`: check for existing record; if deactivated → call `user.Reactivate(accessLevel)` + `UpdateAsync` + append `UserAuditEntry(Reactivated)`; if active → throw `UserConflictException`; if not found → `AuthorizedUser.Create(...)` + `AddAsync` + append `UserAuditEntry(Created)`; return dto — in `src/ProxyManager.API/Handlers/CreateAuthorizedUserHandler.cs` (depends on T035)
- [ ] T038 [P] [US2] Implement `UpdateUserAccessLevelHandler`: get user by sub; throw `UserNotFoundException` if not found or deactivated; call `user.UpdateAccessLevel(newLevel)` + `UpdateAsync`; append `UserAuditEntry(Updated)` with previous/new levels; return dto — in `src/ProxyManager.API/Handlers/UpdateUserAccessLevelHandler.cs` (depends on T036)

### Endpoint & Integration

- [ ] T039 [US2] Add `POST /` and `PATCH /{sub}` endpoints to `UserEndpoints.cs`: `POST` requires `"UserAdmin"` policy; on `UserConflictException` → 409; on `UserValidationException` → 400; reactivation path → 200 with `X-User-Reactivated: true` header; new user → 201 with `Location: /v1/users/{sub}`; `PATCH` requires `"UserAdmin"` policy; on `UserNotFoundException` → 404 — in `src/ProxyManager.API/Endpoints/UserEndpoints.cs` (depends on T037, T038)
- [ ] T040 [US2] Write integration tests for `POST /v1/users` (201 new user, 200 reactivation, 409 conflict, 400 bad body, 401 no token, 403 ReadOnly token) and `PATCH /v1/users/{sub}` (200 updated, 404 not found, 403 ReadOnly token, 401 no token) — in `tests/ProxyManager.API.Tests/Integration/UserEndpointsTests.cs` (depends on T039, T002)

**Checkpoint**: Create and update endpoints functional; audit entries written; reactivation path works; role enforcement verified.

---

## Phase 5: User Story 3 — Revoke User Access (Priority: P3)

**Goal**: Admin clients can soft-delete (deactivate) an active user. The user's record is retained but they no longer appear in the default active-user list.

**Independent Test**: Seed an active user; call `DELETE /v1/users/{sub}` with Admin token; assert 204; call `GET /v1/users/{sub}` and assert the user has `status: "Deactivated"`. Call DELETE again; assert 404.

### Handler Tests — Red Phase ⚠️

> **Write this test FIRST. Confirm it FAILS before T043.**

- [ ] T041 [US3] Write `DeactivateUserHandlerTests` covering: active user → sets `Status=Deactivated`, sets `DeactivatedAt`, appends `UserAuditEntry(Deactivated)`, returns void; unknown or already-deactivated sub → throws `UserNotFoundException`; audit failure → propagates exception — in `tests/ProxyManager.API.Tests/Unit/Handlers/DeactivateUserHandlerTests.cs`

### Handler Implementation — Green Phase

- [ ] T042 [US3] Implement `DeactivateUserHandler`: get user by sub; throw `UserNotFoundException` if not found or `Status == Deactivated`; call `user.Deactivate()` + `UpdateAsync`; append `UserAuditEntry(Deactivated)` with previous access level — in `src/ProxyManager.API/Handlers/DeactivateUserHandler.cs` (depends on T041)

### Endpoint & Integration

- [ ] T043 [US3] Add `DELETE /{sub}` endpoint to `UserEndpoints.cs` requiring `"UserAdmin"` policy; on success → 204 No Content; on `UserNotFoundException` → 404 Problem Details — in `src/ProxyManager.API/Endpoints/UserEndpoints.cs` (depends on T042)
- [ ] T044 [US3] Write integration tests for `DELETE /v1/users/{sub}` (204 on active user, 404 on missing or already-deactivated user, 403 with ReadOnly token, 401 with no token); verify deactivated user no longer appears in default list but appears with `includeDeactivated=true` — in `tests/ProxyManager.API.Tests/Integration/UserEndpointsTests.cs` (depends on T043, T002)

**Checkpoint**: Deactivation endpoint functional; soft-delete verified; audit entry written; already-deactivated returns 404.

---

## Phase 6: User Story 4 — Audit Log Query (Priority: P4)

**Goal**: Any authenticated client (Admin or ReadOnly) can retrieve a paginated, filterable audit log of all user management operations.

**Independent Test**: Perform a create and a deactivate via the API; call `GET /v1/users/audit`; assert 2 entries in chronological order. Filter by `sub`; assert only matching entries. Filter by `from` date; assert only entries after the date.

### Handler Tests — Red Phase ⚠️

> **Write this test FIRST. Confirm it FAILS before T047.**

- [ ] T045 [US4] Write `GetUserAuditLogHandlerTests` covering: returns paginated `PagedResult<UserAuditEntryDto>`; filters by `SubFilter` (only matching `SubjectSub`); filters by `From` and `To` date range; returns empty result when no entries match; maps all DTO fields — in `tests/ProxyManager.API.Tests/Unit/Handlers/GetUserAuditLogHandlerTests.cs`

### Handler Implementation — Green Phase

- [ ] T046 [US4] Implement `GetUserAuditLogHandler` handling `GetUserAuditLogQuery` → `PagedResult<UserAuditEntryDto>`; delegates filtering to `IUserAuditRepository.QueryAsync` — in `src/ProxyManager.API/Handlers/GetUserAuditLogHandler.cs` (depends on T045)

### Endpoint & Integration

- [ ] T047 [US4] `GET /audit` endpoint already declared in `UserEndpoints.cs` (T032); complete its handler wiring to `GetUserAuditLogQuery`; accept query parameters `sub`, `from`, `to`, `page`, `pageSize` — in `src/ProxyManager.API/Endpoints/UserEndpoints.cs` (depends on T046)
- [ ] T048 [US4] Write integration tests for `GET /v1/users/audit` (200 all entries, filter by sub, filter by date range, 401 no token); verify ReadOnly token receives 200 (not 403) — in `tests/ProxyManager.API.Tests/Integration/UserEndpointsTests.cs` (depends on T047, T002)

**Checkpoint**: All four user stories complete. Full feature functional end-to-end.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T049 [P] Add structured Serilog log events (using `Log.Information(...)` with named properties) for each user management operation (created, updated, deactivated, reactivated) in each handler in `src/ProxyManager.API/Handlers/`
- [ ] T050 [P] Verify XML documentation is present and complete on all public types: `AuthorizedUser`, `UserAuditEntry`, `IAuthorizedUserRepository`, `IUserAuditRepository`, `AuthorizedUserDto`, `UserAuditEntryDto`, all endpoint request records in `src/ProxyManager.API/Endpoints/UserEndpoints.cs`
- [ ] T051 Run `dotnet test --collect:"XPlat Code Coverage"` and confirm ≥ 80% line coverage for all new files in `src/ProxyManager.Core/AggregatesModel/UserAggregate/`, `src/ProxyManager.Infrastructure/Repositories/`, and `src/ProxyManager.API/Handlers/`
- [ ] T052 [P] Validate `specs/003-user-mgmt-api/quickstart.md` curl examples against a locally running API; confirm all expected status codes

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)        → No dependencies; start immediately
Phase 2 (Foundational) → Depends on Phase 1; BLOCKS all user story phases
Phase 3 (US1 - Query)  → Depends on Phase 2; MVP — stop here to validate
Phase 4 (US2 - Write)  → Depends on Phase 2; can run in parallel with Phase 3
Phase 5 (US3 - Delete) → Depends on Phase 2; can run in parallel with Phase 3/4
Phase 6 (US4 - Audit)  → Depends on Phase 2; audit log endpoint wiring depends on Phase 3 (T032)
Phase 7 (Polish)       → Depends on all story phases being complete
```

### User Story Dependencies

- **US1 (P1)**: Only foundational phase; no dependency on other stories
- **US2 (P2)**: Only foundational phase; writes to same endpoint file as US1 (T032/T039 must not overlap)
- **US3 (P3)**: Only foundational phase; appends to same endpoint file (T043 after T039)
- **US4 (P4)**: `GET /audit` route declared in US1 (T032); handler wired in T047

### Within Each Story

```
Red (tests)  →  Green (implementation)  →  Endpoint  →  Integration tests
```

### Parallel Opportunities per Phase

**Phase 2 (Foundational)**:
```
Parallel group A (T003–T008):  All enums and exceptions
Parallel group B (T014–T022):  All DTOs and message types
Sequential:  T009 (write aggregate tests) → T010 (implement aggregate) → T011 → T012 → T013
Sequential:  T023 → T024 (repos, after T010–T013)
Parallel:    T025, T026 (fakes, after T012–T013)
```

**Phase 3 (US1)**:
```
Parallel:  T028, T029 (write handler tests)
Parallel:  T030, T031 (implement handlers, after their respective Red tasks)
Sequential: T032 → T033 → T034
```

**Phase 4 (US2)**:
```
Parallel:  T035, T036 (write handler tests)
Parallel:  T037, T038 (implement handlers)
Sequential: T039 → T040
```

---

## Parallel Example: Foundational Phase

```
# Run in parallel — no dependencies between these:
T003: UserAccessLevel.cs
T004: UserStatus.cs
T005: UserOperation.cs
T006: UserNotFoundException.cs
T007: UserConflictException.cs
T008: UserValidationException.cs
T014: AuthorizedUserDto.cs
T015: UserAuditEntryDto.cs
T016–T022: All message types

# Sequential after enums:
T009 (write aggregate tests — Red)
T010 (implement AuthorizedUser — Green)
T011 (implement UserAuditEntry)
T012 (IAuthorizedUserRepository)
T013 (IUserAuditRepository)
T023 (InMemoryAuthorizedUserRepository)
T024 (InMemoryUserAuditRepository)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (Setup — T001–T002)
2. Complete Phase 2 (Foundational — T003–T027)
3. Complete Phase 3 (US1 — T028–T034)
4. **STOP and VALIDATE**: `GET /v1/users` and `GET /v1/users/{sub}` work end-to-end
5. Demo to stakeholders

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 (read-only API) → Independently testable → **Demo**
3. US2 (create/update) → Independently testable → **Demo**
4. US3 (deactivate) → Independently testable → **Demo**
5. US4 (audit log) → Full feature complete → **Demo**
6. Polish → Production ready

### Parallel Team Strategy

```
After Phase 2 completes:
  Developer A: Phase 3 (US1 — Query)
  Developer B: Phase 4 (US2 — Write)
  Developer C: Phase 5 (US3 — Deactivate)
  (US4 waits on Phase 3 endpoint scaffold)
```

---

## Notes

- Every Red phase task MUST be run with `dotnet test` to confirm failure before proceeding to Green
- `GET /audit` is declared BEFORE `GET /{sub}` in `UserEndpoints.cs` to ensure ASP.NET Core route matching selects the literal segment first
- `ProfileImageUrl` validated in `AuthorizedUser.Create()` via `Uri.TryCreate(..., UriKind.Absolute, out _)` + scheme check; not fetched
- `sub` route parameter is typed as `string` (not `Guid`) to honour the opaque-identifier contract
- Actor `sub` for audit entries: `user.FindFirstValue("sub") ?? "unknown"` (consistent with existing `ProxyHostEndpoints.cs` pattern)
- Commit after each task or logical group; do not batch across stories
