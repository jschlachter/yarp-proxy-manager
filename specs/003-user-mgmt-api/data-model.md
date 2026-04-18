# Data Model: User Management API (003-user-mgmt-api)

> **Revision note (2026-04-18)**: Added `Nickname` and optional `ProfileImageUrl` fields to `AuthorizedUser` and all downstream DTOs/contracts per user clarification.

## Enumerations

### `UserAccessLevel`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/UserAccessLevel.cs`

```
Admin     — May perform all create, update, deactivate, and audit-log-query operations
ReadOnly  — May only perform read operations (list, get, query audit log)
```

### `UserStatus`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/UserStatus.cs`

```
Active       — User is authorized to access Proxy Manager
Deactivated  — User's access has been revoked; record is retained for audit purposes
```

### `UserOperation`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/UserOperation.cs`

```
Created      — New user record was added
Updated      — User's access level was changed
Deactivated  — User's access was revoked (soft delete)
Reactivated  — A previously deactivated user's access was restored
```

---

## Entities

### `AuthorizedUser`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/AuthorizedUser.cs`
Aggregate root.

| Field | Type | Required | Constraints | Notes |
|---|---|---|---|---|
| `Sub` | `string` | Yes | Non-empty, unique among Active records | Authentik subject claim; treated as opaque string; primary identifier |
| `DisplayName` | `string` | Yes | Non-empty | Full/formal name (e.g., "Alice Anderson") |
| `Nickname` | `string` | Yes | Non-empty | Preferred short name for display (e.g., "Alice") |
| `Email` | `string` | Yes | Non-empty | Email address; informational, not used as identifier |
| `ProfileImageUrl` | `string?` | No | Nullable; must be a valid absolute URL if provided | Link to external profile image (Authentik avatar, Gravatar, etc.) |
| `AccessLevel` | `UserAccessLevel` | Yes | `Admin` or `ReadOnly` | Controls which operations the user may perform |
| `Status` | `UserStatus` | Yes | `Active` or `Deactivated`; default `Active` on creation | |
| `CreatedAt` | `DateTimeOffset` | Yes | Set on creation; immutable | UTC |
| `LastModifiedAt` | `DateTimeOffset` | Yes | Updated on any mutation | UTC |
| `DeactivatedAt` | `DateTimeOffset?` | No | Nullable; set on deactivation, cleared on reactivation | UTC |

**State transitions**:
```
[not exist] --Create--> Active
Active --Deactivate--> Deactivated
Deactivated --Reactivate (via POST)--> Active
Active --UpdateAccessLevel--> Active (LastModifiedAt updated)
```

**Uniqueness rule**: Two records with the same `Sub` MUST NOT both have `Status = Active`. A deactivated record may share `Sub` with a future active record (it IS the same record, reactivated).

**Validation rules**:
- `Sub`, `DisplayName`, `Nickname`, `Email` MUST be non-null and non-whitespace
- `ProfileImageUrl`, if provided, MUST be a valid absolute `http` or `https` URL
- `AccessLevel` MUST be a defined enum value (`Admin` or `ReadOnly`)

---

### `UserAuditEntry`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/UserAuditEntry.cs`
Append-only; no mutations after creation.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `Guid` | Auto-generated | Surrogate key |
| `SubjectSub` | `string` | Required, non-empty | The `Sub` of the affected `AuthorizedUser` |
| `Operation` | `UserOperation` | Required | Created / Updated / Deactivated / Reactivated |
| `PreviousAccessLevel` | `UserAccessLevel?` | Nullable | Populated for `Updated` and `Deactivated` operations |
| `NewAccessLevel` | `UserAccessLevel?` | Nullable | Populated for `Created`, `Updated`, and `Reactivated` operations |
| `ActorSub` | `string` | Required, non-empty | `sub` claim of the JWT that triggered the operation |
| `OccurredAt` | `DateTimeOffset` | Set at creation; immutable | UTC |

---

## DTOs

### `AuthorizedUserDto`
Location: `ProxyManager.Core/DTOs/AuthorizedUserDto.cs`

```csharp
record AuthorizedUserDto(
    string Sub,
    string DisplayName,
    string Nickname,
    string Email,
    string? ProfileImageUrl,
    string AccessLevel,        // "Admin" or "ReadOnly"
    string Status,             // "Active" or "Deactivated"
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt,
    DateTimeOffset? DeactivatedAt
);
```

### `UserAuditEntryDto`
Location: `ProxyManager.Core/DTOs/UserAuditEntryDto.cs`

```csharp
record UserAuditEntryDto(
    Guid Id,
    string SubjectSub,
    string Operation,            // "Created" | "Updated" | "Deactivated" | "Reactivated"
    string? PreviousAccessLevel,
    string? NewAccessLevel,
    string ActorSub,
    DateTimeOffset OccurredAt
);
```

---

## Repository Interfaces

### `IAuthorizedUserRepository`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/IAuthorizedUserRepository.cs`

```csharp
interface IAuthorizedUserRepository
{
    Task<AuthorizedUser?> GetBySubAsync(string sub, CancellationToken ct = default);
    Task<PagedResult<AuthorizedUser>> GetAllAsync(bool includeDeactivated, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(AuthorizedUser user, CancellationToken ct = default);
    Task UpdateAsync(AuthorizedUser user, CancellationToken ct = default);
}
```

### `IUserAuditRepository`
Location: `ProxyManager.Core/AggregatesModel/UserAggregate/IUserAuditRepository.cs`

```csharp
interface IUserAuditRepository
{
    Task AppendAsync(UserAuditEntry entry, CancellationToken ct = default);
    Task<PagedResult<UserAuditEntry>> QueryAsync(
        string? subFilter,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
```

---

## Messages (Wolverine Commands / Queries)

### Commands
Location: `ProxyManager.Core/Messages/Commands/`

```
CreateAuthorizedUserCommand(Sub, DisplayName, Nickname, Email, ProfileImageUrl?, AccessLevel, ActorSub)
UpdateUserAccessLevelCommand(Sub, NewAccessLevel, ActorSub)
DeactivateUserCommand(Sub, ActorSub)
```

### Queries
Location: `ProxyManager.Core/Messages/Queries/`

```
GetAuthorizedUsersQuery(IncludeDeactivated, Page, PageSize)
GetAuthorizedUserBySubQuery(Sub)
GetUserAuditLogQuery(SubFilter?, From?, To?, Page, PageSize)
```

### Events
Location: `ProxyManager.Core/Messages/Events/`

```
UserCreatedEvent(Sub, AccessLevel, ActorSub)
UserAccessLevelUpdatedEvent(Sub, PreviousAccessLevel, NewAccessLevel, ActorSub)
UserDeactivatedEvent(Sub, ActorSub)
UserReactivatedEvent(Sub, NewAccessLevel, ActorSub)
```

---

## Authorization Policy

**Policy name**: `"UserAdmin"`

**Requirement**: JWT must contain claim `pm_role` with value `"Admin"`.

**Applied to**: All write endpoints (`POST`, `PATCH`, `DELETE` on `/v1/users`).

**Read endpoints** (`GET /v1/users`, `GET /v1/users/{sub}`, `GET /v1/users/audit`) use `RequireAuthorization()` only — any authenticated user (Admin or ReadOnly) may access them.

---

## Source Tree (this feature)

```
src/
└── ProxyManager.Core/
    ├── AggregatesModel/
    │   └── UserAggregate/
    │       ├── AuthorizedUser.cs
    │       ├── IAuthorizedUserRepository.cs
    │       ├── IUserAuditRepository.cs
    │       ├── UserAccessLevel.cs
    │       ├── UserAuditEntry.cs
    │       ├── UserOperation.cs
    │       └── UserStatus.cs
    ├── DTOs/
    │   ├── AuthorizedUserDto.cs
    │   └── UserAuditEntryDto.cs
    ├── Exceptions/
    │   ├── UserConflictException.cs
    │   ├── UserNotFoundException.cs
    │   └── UserValidationException.cs
    └── Messages/
        ├── Commands/
        │   ├── CreateAuthorizedUserCommand.cs
        │   ├── DeactivateUserCommand.cs
        │   └── UpdateUserAccessLevelCommand.cs
        ├── Events/
        │   ├── UserAccessLevelUpdatedEvent.cs
        │   ├── UserCreatedEvent.cs
        │   ├── UserDeactivatedEvent.cs
        │   └── UserReactivatedEvent.cs
        └── Queries/
            ├── GetAuthorizedUserBySubQuery.cs
            ├── GetAuthorizedUsersQuery.cs
            └── GetUserAuditLogQuery.cs

src/
└── ProxyManager.Infrastructure/
    └── Repositories/
        ├── InMemoryAuthorizedUserRepository.cs
        └── InMemoryUserAuditRepository.cs

src/
└── ProxyManager.API/
    ├── Endpoints/
    │   └── UserEndpoints.cs
    └── Handlers/
        ├── CreateAuthorizedUserHandler.cs
        ├── DeactivateUserHandler.cs
        ├── GetAuthorizedUserBySubHandler.cs
        ├── GetAuthorizedUsersHandler.cs
        ├── GetUserAuditLogHandler.cs
        └── UpdateUserAccessLevelHandler.cs

tests/
└── ProxyManager.API.Tests/
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

tests/
└── ProxyManager.Core.Tests/
    └── Unit/
        └── AuthorizedUserAggregateTests.cs
```
