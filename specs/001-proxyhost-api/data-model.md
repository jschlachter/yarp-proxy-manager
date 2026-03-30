# Data Model: Proxy Host Management API

**Branch**: `001-proxyhost-api` | **Date**: 2026-03-21

---

## Existing Entities (unchanged)

### ProxyHost *(aggregate root — existing)*

Location: `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/ProxyHost.cs`
Namespace: `West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Identity, assigned on Create |
| DomainNames | IReadOnlyList\<string\> | One or more public hostnames; min 1 required |
| Destination | DestinationUri | Upstream backend address (scheme + host + port) |
| IsEnabled | bool | Active/inactive toggle; defaults to `true` on Create |
| Certificate | ProxyCertificate? | Optional TLS certificate config |

**Mutations**:
- `ProxyHost.Create(domainNames, destination, certificate?)` — factory method, sets Id and IsEnabled=true
- `Enable()` / `Disable()` — toggle active status
- `UpdateDestination(destination)` — replace upstream address
- `UpdateDomainNames(domainNames)` — replace domain name list
- `SetCertificate(certificate?)` — set or clear certificate

**Validation rules** (existing):
- At least one domain name required
- DestinationUri scheme must be `http` or `https`
- DestinationUri port must be 1–65535

---

### DestinationUri *(value object — existing)*

Location: `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/DestinationUri.cs`

| Field | Type | Notes |
|-------|------|-------|
| Scheme | string | `"http"` or `"https"` |
| Host | string | Upstream hostname |
| Port | int | 1–65535; defaults to 80/443 when parsing a standard URL |

`ToString()` renders as `"scheme://host:port"`.
`DestinationUri.Parse(string)` parses from an absolute URI string.

---

### ProxyCertificate *(value object — existing)*

Location: `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/ProxyCertificate.cs`

| Field | Type | Notes |
|-------|------|-------|
| CertificatePath | string | Path to the certificate file |
| KeyPath | string? | Optional path to the private key file |
| Password | string? | Optional certificate password |

---

## New Entities

### AuditLogEntry *(immutable record — new)*

Location: `src/ProxyManager.Core/AggregatesModel/AuditLogAggregate/AuditLogEntry.cs`
Namespace: `West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Unique entry identifier |
| ActorId | string | JWT `sub` claim of the caller |
| Operation | AuditOperation | Enum: `Created`, `Updated`, `Deleted` |
| ProxyHostId | Guid | The affected ProxyHost |
| PreviousState | string? | JSON snapshot of state before change; null for Create |
| NewState | string? | JSON snapshot of state after change; null for Delete |
| OccurredAt | DateTimeOffset | UTC timestamp of the operation |

`AuditLogEntry` is created via a static factory `Create(...)`. It is immutable after creation.

**Validation rules**:
- `ActorId` must not be null or whitespace
- `OccurredAt` must be in UTC

---

## DTOs (API surface)

### ProxyHostDto *(record — new)*

Location: `src/ProxyManager.Core/DTOs/ProxyHostDto.cs`
Namespace: `West94.ProxyManager.Core.DTOs`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | |
| DomainNames | IReadOnlyList\<string\> | |
| Destination | string | `"scheme://host:port"` string representation |
| IsEnabled | bool | |
| Certificate | ProxyCertificateDto? | Omits password from response |

### ProxyCertificateDto *(record — new)*

| Field | Type | Notes |
|-------|------|-------|
| CertificatePath | string | |
| KeyPath | string? | |

*Password is intentionally excluded from the DTO.*

### PagedResult\<T\> *(record — new)*

Location: `src/ProxyManager.Core/DTOs/PagedResult.cs`
Namespace: `West94.ProxyManager.Core.DTOs`

| Field | Type | Notes |
|-------|------|-------|
| Items | IReadOnlyList\<T\> | |
| TotalCount | int | Total items across all pages |
| Page | int | Current page (1-based) |
| PageSize | int | Items per page |

---

## CQRS Messages

### Queries

**GetProxyHostsQuery**
Location: `src/ProxyManager.Core/Messages/Queries/GetProxyHostsQuery.cs`

```
record GetProxyHostsQuery(int Page = 1, int PageSize = 20)
```
Returns: `PagedResult<ProxyHostDto>`

**GetProxyHostByIdQuery**
Location: `src/ProxyManager.Core/Messages/Queries/GetProxyHostByIdQuery.cs`

```
record GetProxyHostByIdQuery(Guid Id)
```
Returns: `ProxyHostDto?`

---

### Commands

**CreateProxyHostCommand**
Location: `src/ProxyManager.Core/Messages/Commands/CreateProxyHostCommand.cs`

```
record CreateProxyHostCommand(
    IEnumerable<string> DomainNames,
    string DestinationUri,
    string? CertificatePath,
    string? CertificateKeyPath,
    string ActorId)
```
Returns: `ProxyHostDto`

**UpdateProxyHostCommand**
Location: `src/ProxyManager.Core/Messages/Commands/UpdateProxyHostCommand.cs`

```
record UpdateProxyHostCommand(
    Guid Id,
    IEnumerable<string>? DomainNames,
    string? DestinationUri,
    bool? IsEnabled,
    string? CertificatePath,
    string? CertificateKeyPath,
    string ActorId)
```
Returns: `ProxyHostDto`

**DeleteProxyHostCommand**
Location: `src/ProxyManager.Core/Messages/Commands/DeleteProxyHostCommand.cs`

```
record DeleteProxyHostCommand(Guid Id, string ActorId)
```
Returns: void

---

## Integration Events (RabbitMQ)

### ProxyHostCreatedEvent
Location: `src/ProxyManager.Core/Messages/Events/ProxyHostCreatedEvent.cs`

```
record ProxyHostCreatedEvent(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    string Destination,
    bool IsEnabled,
    DateTimeOffset OccurredAt)
```

### ProxyHostUpdatedEvent
Location: `src/ProxyManager.Core/Messages/Events/ProxyHostUpdatedEvent.cs`

```
record ProxyHostUpdatedEvent(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    string Destination,
    bool IsEnabled,
    DateTimeOffset OccurredAt)
```

### ProxyHostDeletedEvent
Location: `src/ProxyManager.Core/Messages/Events/ProxyHostDeletedEvent.cs`

```
record ProxyHostDeletedEvent(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    DateTimeOffset OccurredAt)
```

---

## Repository Interfaces

### IAuditLogRepository *(new)*

Location: `src/ProxyManager.Core/AggregatesModel/AuditLogAggregate/IAuditLogRepository.cs`

```
Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default);
Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(Guid proxyHostId, CancellationToken ct = default);
Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
```

### IProxyHostRepository *(existing — no changes needed)*

Already has: `FindAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`.

---

## State Transitions

```
                 Create
[none] ─────────────────────▶ ProxyHost (IsEnabled=true)
                                │         │
                         Enable │         │ Disable
                                │         │
                         ProxyHost ◀──────┘
                         (IsEnabled=true/false)
                                │
                         Delete │
                                ▼
                            [removed]
```

Each state transition (Create, Update fields, Enable, Disable, Delete) produces an `AuditLogEntry`
and publishes an integration event to RabbitMQ.
