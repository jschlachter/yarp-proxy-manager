# Implementation Plan: User Management API

**Branch**: `003-user-mgmt-api` | **Date**: 2026-04-18 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/003-user-mgmt-api/spec.md`

> **Revision note (2026-04-18)**: Updated to incorporate user-provided field clarifications: `Nickname` and optional `ProfileImageUrl` added to `AuthorizedUser`. All downstream artifacts (data-model.md, contracts, quickstart) updated accordingly.

## Summary

Add a versioned REST API (`/v1/users`) to ProxyManager.API for querying and managing authorized Proxy Manager users. The API reuses the existing JWT Bearer / Authentik authentication and adds a `pm_role` claim-based authorization policy to gate write operations to Admin-role clients. Users are modeled as a new `AuthorizedUser` aggregate in `ProxyManager.Core`, backed by in-memory repositories in `ProxyManager.Infrastructure` (matching the current storage pattern). User profiles capture: Authentik `sub` (unique identifier), DisplayName (full name), Nickname, Email, and an optional ProfileImageUrl. Every mutation (create, update, deactivate, reactivate) produces a `UserAuditEntry` written atomically in the same handler as the user operation. Deletion is soft-delete (status flag); reactivation is handled through the POST endpoint.

## Technical Context

**Language/Version**: C# / .NET 10.0  
**Primary Dependencies**: WolverineFx 5.22.x (command/query bus), Microsoft.AspNetCore.Authentication.JwtBearer 10.0.x, Serilog 4.3.x, xUnit 2.9.x, Microsoft.AspNetCore.Mvc.Testing 10.0.x  
**Storage**: In-memory `ConcurrentDictionary` (consistent with existing feature 001 pattern; persistent store deferred)  
**Testing**: xUnit + `WebApplicationFactory<Program>` (existing `TestWebAppFactory` / `TestJwtFactory` extended with role claims)  
**Target Platform**: Linux server / ASP.NET Core 10  
**Project Type**: web-service (Minimal API endpoint group added to existing ProxyManager.API)  
**Performance Goals**: Management API endpoints < 200 ms p95 (Constitution Principle IV)  
**Constraints**: No `IConfiguration` injected directly; `IOptions<T>` for any new config; file-scoped namespaces; primary constructors; `async Task` only  
**Scale/Scope**: Tens to hundreds of operator accounts; 50 concurrent management requests (SC-005)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Gate | Principle | Status |
|---|---|---|
| Tests written failing before implementation begins (Red-Green-Refactor) | II. Testing Standards | PASS — tasks ordered tests-first; handler unit test tasks precede implementation tasks |
| API error responses use RFC 9457 Problem Details (`application/problem+json`) | III. User Experience Consistency | PASS — all endpoints use `TypedResults.Problem(...)`, consistent with existing `ProxyHostEndpoints` |
| Performance goals and latency budgets documented in Technical Context | IV. Performance Requirements | PASS — 200 ms p95 documented above; SC-005 (50 concurrent) in spec |
| No raw `IConfiguration` injection; `IOptions<T>` throughout | I. Code Quality | PASS — no new configuration sections requiring `IOptions<T>`; JWT auth config already wired in `Program.cs` |
| All list endpoints support pagination; unbounded result sets forbidden | III. User Experience Consistency | PASS — `GET /v1/users` and `GET /v1/users/audit` both paginated via `PagedResult<T>` |
| XML documentation on all public APIs | I. Code Quality | PASS — task list includes XML doc requirement on entity, repo interface, DTO, and endpoint files |

**Post-design re-check**: All gates remain green. The `"UserAdmin"` authorization policy and route group structure introduce no violations.

## Project Structure

### Documentation (this feature)

```text
specs/003-user-mgmt-api/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── users-v1.md     ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code

```text
src/ProxyManager.Core/
└── AggregatesModel/
    └── UserAggregate/
        ├── AuthorizedUser.cs           (aggregate root)
        ├── IAuthorizedUserRepository.cs
        ├── IUserAuditRepository.cs
        ├── UserAccessLevel.cs          (enum: Admin, ReadOnly)
        ├── UserAuditEntry.cs
        ├── UserOperation.cs            (enum: Created, Updated, Deactivated, Reactivated)
        └── UserStatus.cs               (enum: Active, Deactivated)
└── DTOs/
    ├── AuthorizedUserDto.cs
    └── UserAuditEntryDto.cs
└── Exceptions/
    ├── UserConflictException.cs
    ├── UserNotFoundException.cs
    └── UserValidationException.cs
└── Messages/
    ├── Commands/
    │   ├── CreateAuthorizedUserCommand.cs
    │   ├── DeactivateUserCommand.cs
    │   └── UpdateUserAccessLevelCommand.cs
    ├── Events/
    │   ├── UserCreatedEvent.cs
    │   ├── UserAccessLevelUpdatedEvent.cs
    │   ├── UserDeactivatedEvent.cs
    │   └── UserReactivatedEvent.cs
    └── Queries/
        ├── GetAuthorizedUsersQuery.cs
        ├── GetAuthorizedUserBySubQuery.cs
        └── GetUserAuditLogQuery.cs

src/ProxyManager.Infrastructure/
└── Repositories/
    ├── InMemoryAuthorizedUserRepository.cs
    └── InMemoryUserAuditRepository.cs

src/ProxyManager.API/
├── Endpoints/
│   └── UserEndpoints.cs               (MapGroup("/v1/users"))
└── Handlers/
    ├── CreateAuthorizedUserHandler.cs
    ├── DeactivateUserHandler.cs
    ├── GetAuthorizedUserBySubHandler.cs
    ├── GetAuthorizedUsersHandler.cs
    ├── GetUserAuditLogHandler.cs
    └── UpdateUserAccessLevelHandler.cs

tests/ProxyManager.Core.Tests/
└── Unit/
    └── AuthorizedUserAggregateTests.cs

tests/ProxyManager.API.Tests/
├── Integration/
│   └── UserEndpointsTests.cs
└── Unit/
    ├── Fakes/
    │   ├── FakeAuthorizedUserRepository.cs
    │   └── FakeUserAuditRepository.cs
    └── Handlers/
        ├── CreateAuthorizedUserHandlerTests.cs
        ├── DeactivateUserHandlerTests.cs
        ├── GetAuthorizedUserBySubHandlerTests.cs
        ├── GetAuthorizedUsersHandlerTests.cs
        ├── GetUserAuditLogHandlerTests.cs
        └── UpdateUserAccessLevelHandlerTests.cs
```

**Structure Decision**: Single-project extension of the existing `ProxyManager.API` service. No new project is needed. All new domain types land in `ProxyManager.Core` and infrastructure in `ProxyManager.Infrastructure`, following the existing layering exactly. The `UserAggregate` namespace mirrors `ProxyHostAggregate`.

## Implementation Phases

### Phase 1 — Core Domain & Repository Contracts
Add `UserAggregate` entity, enums, exceptions, repository interfaces, DTOs, and message types to `ProxyManager.Core`. Write `ProxyManager.Core.Tests` aggregate tests first (Red phase).

### Phase 2 — In-Memory Repositories & Fakes
Implement `InMemoryAuthorizedUserRepository` and `InMemoryUserAuditRepository` in `ProxyManager.Infrastructure`. Implement `FakeAuthorizedUserRepository` and `FakeUserAuditRepository` in the test project for handler unit tests.

### Phase 3 — Wolverine Handlers
Implement the six Wolverine handlers in `ProxyManager.API/Handlers/`. Write unit tests first (Red phase using fakes), then implement (Green phase). Each handler writes to both the user repository and the audit repository before returning.

### Phase 4 — Endpoint Registration & Authorization Policy
Implement `UserEndpoints.cs` with the `MapGroup("/v1/users")` route group. Register the `"UserAdmin"` authorization policy in `ServiceCollectionExtensions`. Wire repositories into DI. Register endpoint mapping in `Program.cs`.

### Phase 5 — Integration Tests & Coverage
Extend `TestJwtFactory` with a `CreateToken(sub, pmRole)` overload to support role-differentiated tokens. Write `UserEndpointsTests.cs` covering all happy-path and error scenarios from the spec acceptance scenarios. Verify ≥ 80% line coverage for new code.

## Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Role claim name | `pm_role` | Avoids collision with Authentik's own `roles`/`groups` claims |
| API versioning | Route prefix `/v1` via `MapGroup` | Zero-dependency; readable in Scalar UI; YAGNI for a single version |
| Soft delete | `Status` enum + `DeactivatedAt` timestamp on entity | Preserves full audit history; cheaper to filter than null-check |
| Reactivation response | `200 OK` + `X-User-Reactivated: true` header | 201 is semantically wrong for restoring an existing resource |
| User identifier in routes | `{sub}` path parameter as opaque string | Removes internal ID indirection; clients always know their own `sub` |
| Audit atomicity | Sequential writes in handler; audit failure throws | In-memory stores; no compensating rollback needed; failure surfaced to caller per FR-009 |
| Profile image | Optional `ProfileImageUrl` stored as a URL string; no image data stored | Keeps the API lightweight; images are hosted externally (e.g., Authentik, Gravatar) |
| Nickname | Separate field from DisplayName | DisplayName = full legal/formal name; Nickname = preferred short name for UI display |

## Complexity Tracking

> No Constitution violations; this section is intentionally blank.
