# Research: Proxy Host Management API

**Branch**: `001-proxyhost-api` | **Date**: 2026-03-21

## Decision 1: Wolverine as CQRS Message Bus

- **Decision**: Use `WolverineFx` (v5.22.x) as the in-process CQRS mediator and out-of-process
  message broker transport.
- **Rationale**: Wolverine provides a unified handler pattern for both in-process commands/queries
  and out-of-process RabbitMQ publishing via `WolverineFx.RabbitMQ`. The existing project already
  includes RabbitMQ in its technology stack (CLAUDE.md). Wolverine's convention-based handler
  discovery eliminates boilerplate registration and its `IMessageBus` interface can be injected
  directly into Minimal API endpoints.
- **Alternatives considered**: MediatR — no native broker transport; requires a second library
  (MassTransit) for RabbitMQ, splitting the messaging concern across two frameworks. Azure Service
  Bus — not part of the declared technology stack.

## Decision 2: CQRS Message Placement

- **Decision**: Command, query, and integration event record types live in
  `ProxyManager.Core/Messages/`. Wolverine handlers live in `ProxyManager.API/Handlers/`.
- **Rationale**: Placing message contracts in Core keeps them technology-agnostic and independently
  referenceable. Handlers in the API project have access to repositories (registered in the DI
  container) and can publish integration events via Wolverine's `IMessageContext`. This mirrors
  the clean-architecture layering already in the solution.
- **Alternatives considered**: Handlers in Core — would require Core to reference infrastructure
  or transport concerns, violating the existing dependency direction.

## Decision 3: Integration Events via WolverineFx.RabbitMQ

- **Decision**: Command handlers publish `ProxyHostCreatedEvent`, `ProxyHostUpdatedEvent`, and
  `ProxyHostDeletedEvent` to a `proxy-hosts` fanout exchange in RabbitMQ using Wolverine's
  `context.PublishAsync()`.
- **Rationale**: Wolverine's RabbitMQ transport publishes from within the handler's message context
  transactionally. Events are separate types from domain events so the external contract can evolve
  independently of the domain model.
- **Alternatives considered**: Publishing domain events directly — tightly couples the domain
  aggregate to infrastructure; rejected per Principle I (no infrastructure in domain code).

## Decision 4: In-Memory Repositories

- **Decision**: Use the existing `InMemoryProxyHostRepository` (already implemented) and add a new
  `InMemoryAuditLogRepository` backed by a `ConcurrentQueue<AuditLogEntry>` for append semantics.
- **Rationale**: Per user direction. Repository interfaces (`IProxyHostRepository`,
  `IAuditLogRepository`) isolate handlers from storage, enabling future migration to PostgreSQL
  without touching command/query handlers.
- **Constraint**: In-memory storage does not survive process restarts. Success criterion SC-005
  (90-day audit log retention) requires a persistent store in a future iteration. This is a known
  limitation of the current implementation scope.
- **Alternatives considered**: PostgreSQL via EF Core — deferred to a future feature.

## Decision 5: Actor Identity from JWT Claims

- **Decision**: The actor identity logged in audit entries is the `sub` claim from the validated
  JWT bearer token, accessed via `ClaimsPrincipal` injected into Minimal API endpoints and passed
  as a field on each command.
- **Rationale**: The API already validates JWT Bearer tokens from Authentik. The `sub` claim is
  the stable, unique identifier for the calling client.

## Decision 6: Problem Details for All Error Responses

- **Decision**: All error responses use RFC 9457 Problem Details via `TypedResults.Problem()`
  with appropriate HTTP status codes (401, 403, 404, 409, 422).
- **Rationale**: Required by Constitution Principle III and FR-010. ASP.NET Core 8+ has built-in
  Problem Details support; no additional library needed.

## Decision 7: Pagination Strategy

- **Decision**: List endpoint accepts `page` (1-based, default 1) and `pageSize` (default 20,
  max 100) query parameters. Response wraps results in `PagedResult<T>` with `TotalCount`, `Page`,
  `PageSize`, and `Items` fields.
- **Rationale**: FR-011 requires pagination. Default page size of 20 is conservative and safe for
  the operator-facing use case. A max of 100 prevents accidental full-table scans.

## Decision 8: Package Versions

| Package | Version | Purpose |
|---------|---------|---------|
| WolverineFx | 5.22.0 | CQRS message bus + handler discovery |
| WolverineFx.RabbitMQ | 5.22.0 | RabbitMQ transport for integration events |
| xunit | 2.9.x | Unit and integration test framework |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.x | In-process integration test host |
