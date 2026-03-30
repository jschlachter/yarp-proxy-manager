# Implementation Plan: Proxy Host Management API

**Branch**: `001-proxyhost-api` | **Date**: 2026-03-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-proxyhost-api/spec.md`

## Summary

Build a secured REST API for full CRUD management of proxy hosts in `ProxyManager.API`. Endpoints
use Wolverine as the CQRS message bus — queries and commands are dispatched via `IMessageBus` to
dedicated handlers. Mutating commands write audit log entries to an in-memory repository and
publish integration events (`ProxyHostCreatedEvent`, `ProxyHostUpdatedEvent`,
`ProxyHostDeletedEvent`) to a RabbitMQ fanout exchange via WolverineFx.RabbitMQ. Storage uses the
existing `InMemoryProxyHostRepository` plus a new `InMemoryAuditLogRepository`. All endpoints
require JWT Bearer authorization. Error responses use RFC 9457 Problem Details.

---

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ASP.NET Core Minimal APIs, WolverineFx 5.22.x, WolverineFx.RabbitMQ 5.22.x, Serilog 4.3.x, xunit 2.9.x, Microsoft.AspNetCore.Mvc.Testing 10.0.x
**Storage**: In-memory (ConcurrentDictionary for ProxyHosts, ConcurrentQueue for AuditLog)
**Testing**: xunit + Microsoft.AspNetCore.Mvc.Testing (integration), xunit (unit)
**Target Platform**: Linux server (Podman container), .NET 10 runtime
**Project Type**: web-service (REST API, management plane)
**Performance Goals**: List ≤500 hosts in <1 s; all endpoints <200 ms p95 (Constitution IV)
**Constraints**: JWT Bearer required on all endpoints; audit log entry within 1 s of operation; Problem Details on all errors; pagination required on list endpoint
**Scale/Scope**: Operator-facing management API; low concurrent usage; in-memory storage (no persistence requirement this iteration)

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked post-design below.*

| Gate | Principle | Status |
|------|-----------|--------|
| Tests written and confirmed failing before implementation | II. Testing Standards | ✅ TDD enforced in task order; test tasks precede implementation tasks |
| API errors use RFC 9457 Problem Details | III. UX Consistency | ✅ `TypedResults.Problem()` for all error paths; 400/401/404/409 documented in contracts |
| Performance goals and latency budgets documented | IV. Performance Requirements | ✅ <200 ms p95, <1 s list documented in Technical Context and SC-004 |
| No raw `IConfiguration` injection; `IOptions<T>` used | I. Code Quality | ✅ `RabbitMqOptions` via `IOptions<T>`; JWT config already uses `IConfiguration` at bootstrap only (acceptable in `Program.cs`) |

**Post-design re-check**: All four gates pass. No complexity violations requiring justification.

---

## Project Structure

### Documentation (this feature)

```text
specs/001-proxyhost-api/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── api-endpoints.md # REST API contract
│   └── events.md        # RabbitMQ integration event contract
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── ProxyManager.Core/
│   ├── AggregatesModel/
│   │   ├── AuditLogAggregate/                    [NEW]
│   │   │   ├── AuditLogEntry.cs
│   │   │   └── IAuditLogRepository.cs
│   │   └── ProxyHostAggregate/                   [EXISTING — no changes to aggregate]
│   │       ├── DestinationUri.cs
│   │       ├── IProxyHostRepository.cs
│   │       ├── ProxyCertificate.cs
│   │       └── ProxyHost.cs
│   ├── DTOs/                                      [NEW]
│   │   ├── PagedResult.cs
│   │   ├── ProxyCertificateDto.cs
│   │   └── ProxyHostDto.cs
│   ├── Messages/                                  [NEW]
│   │   ├── Commands/
│   │   │   ├── CreateProxyHostCommand.cs
│   │   │   ├── DeleteProxyHostCommand.cs
│   │   │   └── UpdateProxyHostCommand.cs
│   │   ├── Events/
│   │   │   ├── ProxyHostCreatedEvent.cs
│   │   │   ├── ProxyHostDeletedEvent.cs
│   │   │   └── ProxyHostUpdatedEvent.cs
│   │   └── Queries/
│   │       ├── GetProxyHostByIdQuery.cs
│   │       └── GetProxyHostsQuery.cs
│   └── SeedWork/                                  [EXISTING]
│       ├── Entity.cs
│       └── IDomainEvent.cs
├── ProxyManager.Infrastructure/
│   └── Repositories/
│       ├── InMemoryProxyHostRepository.cs         [EXISTING]
│       └── InMemoryAuditLogRepository.cs          [NEW]
└── ProxyManager.API/
    ├── Endpoints/
    │   └── ProxyHostEndpoints.cs                  [REPLACES RouteEndpoints.cs stub]
    ├── Handlers/                                  [NEW]
    │   ├── CreateProxyHostHandler.cs
    │   ├── DeleteProxyHostHandler.cs
    │   ├── GetProxyHostByIdHandler.cs
    │   ├── GetProxyHostsHandler.cs
    │   └── UpdateProxyHostHandler.cs
    ├── Infrastructure/                            [NEW]
    │   └── ServiceCollectionExtensions.cs         (DI registration helpers)
    ├── appsettings.json                           [MODIFIED — add RabbitMQ section]
    └── Program.cs                                 [MODIFIED — add Wolverine + repositories]

tests/
├── ProxyManager.API.Tests/                        [NEW project]
│   ├── ProxyManager.API.Tests.csproj
│   ├── Integration/
│   │   ├── ProxyHostEndpointsTests.cs
│   │   └── Helpers/
│   │       ├── TestWebAppFactory.cs
│   │       └── TestJwtFactory.cs
│   └── Unit/
│       └── Handlers/
│           ├── CreateProxyHostHandlerTests.cs
│           ├── DeleteProxyHostHandlerTests.cs
│           ├── GetProxyHostByIdHandlerTests.cs
│           ├── GetProxyHostsHandlerTests.cs
│           └── UpdateProxyHostHandlerTests.cs
└── ProxyManager.Core.Tests/                       [NEW project]
    ├── ProxyManager.Core.Tests.csproj
    └── Unit/
        ├── AuditLogEntryTests.cs
        └── ProxyHostAggregateTests.cs
```

**Structure Decision**: Extends the existing multi-project .NET solution. Message contracts in Core
keep them technology-agnostic. Handlers in the API project have access to repositories and
Wolverine's `IMessageContext`. Tests in a root-level `tests/` directory follow .NET solution
conventions.

---

## Complexity Tracking

No constitution violations requiring justification.
