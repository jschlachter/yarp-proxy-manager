# Feature Specification: Database Persistence for Proxy Configuration

**Feature Branch**: `004-db-persistence`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Add database persistance for proxy configuration information"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Proxy Configuration Survives Restarts (Priority: P1)

As an administrator, I need proxy route and cluster configurations to be saved persistently so that when the proxy service restarts, all routing rules are restored automatically without manual re-entry.

**Why this priority**: Without persistence, all configuration is lost on restart. This is the core value of the feature — configuration must not be ephemeral.

**Independent Test**: Configure several proxy routes, restart the proxy service, and verify all routes are active and routing traffic correctly without any intervention.

**Acceptance Scenarios**:

1. **Given** proxy routes and clusters have been configured via the management API, **When** the proxy service is restarted, **Then** all previously configured routes and clusters are automatically loaded and active.
2. **Given** the database is unavailable at startup, **When** the proxy service attempts to start, **Then** the service fails with a clear error message indicating the database connection issue.
3. **Given** the database contains no configuration on first startup, **When** the proxy service starts, **Then** the service starts successfully with an empty routing table.

---

### User Story 2 - Configuration Changes Take Effect Immediately (Priority: P2)

As an administrator, I need any changes made through the management API (create, update, delete) to proxy routes to be stored in the database **and applied to live traffic immediately**, without requiring a service restart.

**Why this priority**: Administrators must be able to add, update, or remove routes in real time. A restart requirement would make the management API operationally disruptive.

**Independent Test**: Add a new proxy route via the API, then immediately send traffic matching that route — without restarting — and confirm the proxy forwards it correctly.

**Acceptance Scenarios**:

1. **Given** a new proxy route is created via the management API, **When** the API call succeeds, **Then** the route is stored in the database and the proxy begins routing matching traffic within seconds — no restart required.
2. **Given** an existing proxy route is updated via the management API, **When** the update succeeds, **Then** the updated routing behavior is applied immediately to live traffic and persists after a service restart.
3. **Given** an existing proxy route is deleted via the management API, **When** the deletion succeeds, **Then** the proxy immediately stops routing traffic for that route and the deletion persists after a service restart.

---

### User Story 3 - Audit Trail of Configuration Changes (Priority: P3)

As an administrator or auditor, I need a record of who made what configuration changes and when, so that I can diagnose issues and meet compliance requirements.

**Why this priority**: Audit history is valuable for operations but is not required for the system to function correctly.

**Independent Test**: Make several create/update/delete operations via the API, then query the audit log and verify each operation is recorded with a timestamp and the identity of the actor.

**Acceptance Scenarios**:

1. **Given** an administrator creates a proxy route, **When** the creation succeeds, **Then** an audit record is stored capturing the action, the changed data, the actor identity, and the timestamp.
2. **Given** an administrator deletes a proxy route, **When** the deletion succeeds, **Then** an audit record captures the deletion with before-state, actor, and timestamp.
3. **Given** I query the audit log for a specific route, **When** the query returns, **Then** all historical changes to that route appear in chronological order.

---

### Edge Cases

- What happens when a database write fails mid-operation (e.g., route partially saved)? The system must roll back and return an error to the caller; no config-change event is published.
- What happens if the message bus is unavailable when a ProxyHost change is committed? The database write and live routing update succeed; the event publication failure is logged but does not fail the API call (best-effort delivery).
- How does the system behave if the database becomes unavailable after startup? The proxy continues to serve traffic using its last-known live routing state; management API writes return an error until the database recovers. The live routing state is not lost because it is held in memory as the active routing table (DB is the system of record for persistence, not the real-time routing path).
- What happens if the database schema is out of date when the service starts? The service should detect the mismatch and fail with a clear migration error rather than silently producing incorrect behavior.
- What if two instances of the management API try to update the same route concurrently? The last write wins, and both operations are recorded in the audit log.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST persist ProxyHost records to a relational database. A ProxyHost is the high-level abstraction capturing all configuration an administrator needs to define a proxied service (name, hostname/path match, upstream destination(s), routing options).
- **FR-002**: System MUST translate persisted ProxyHost records into the routing engine's native route and cluster configuration at startup and on every change. Administrators never directly manage native route/cluster objects.
- **FR-003**: System MUST load all persisted proxy configuration from the database on service startup and apply it to the active routing table.
- **FR-003b**: On first startup with an empty database, the system MUST seed the database from `proxysettings.{Environment}.json` if that file is present; on all subsequent startups the database is the sole configuration source and the file is ignored.
- **FR-003a**: System MUST apply configuration changes (create, update, delete) to the live routing table immediately upon successful database write, without requiring a service restart.
- **FR-004**: System MUST write configuration changes (create, update, delete) to the database atomically before returning a success response to the caller.
- **FR-005**: System MUST roll back any partial database write and return an error if a write operation fails, leaving no partial or corrupt records.
- **FR-006**: System MUST record an audit log entry for every create, update, and delete operation on a ProxyHost, capturing: action type, changed data (before and after values for updates), actor identity, and UTC timestamp.
- **FR-006a**: System MUST publish a configuration-change event to the message bus after every successful ProxyHost create, update, or delete operation, so that downstream services can react to configuration changes.
- **FR-007**: System MUST expose an API endpoint to retrieve audit log entries, filterable by ProxyHost ID and time range.
- **FR-007a**: System MUST automatically purge audit log entries older than a configurable retention period. The default retention period is 90 days. Administrators MUST be able to override this value via configuration (environment variable or settings file) without code changes.
- **FR-008**: System MUST run any required database schema migrations automatically on startup, or provide a clear operator-run migration command.
- **FR-009**: System MUST NOT lose or corrupt existing proxy configuration during a schema migration.
- **FR-010**: System MUST support configuration for database connection details (host, port, credentials, database name) via environment variables or configuration files.

### Key Entities

- **ProxyHost**: The primary persisted abstraction for a proxied service — host ID, a human-readable name, the public hostname/path match, the upstream destination(s), and any routing options (path transforms, health check settings, enabled flag). This is the entity administrators create and manage. At runtime the system translates ProxyHost records into the underlying routing engine's native configuration.
- **AuditLogEntry**: Records a configuration change event — entry ID, entity type, entity ID, action (created/updated/deleted), actor identity, before-state, after-state, UTC timestamp.

> Note: The routing engine's internal route and cluster concepts (used for traffic forwarding) are derived from ProxyHost records and are not independently persisted. Administrators work exclusively with ProxyHost.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All proxy route and cluster configuration created via the management API persists across service restarts with zero data loss.
- **SC-001a**: Configuration changes (create, update, delete) are reflected in live proxy routing within 2 seconds of the API call succeeding, with no service restart.
- **SC-002**: Configuration changes are durable within the same transaction that returns a success response — no change is acknowledged to the caller without being written to the database.
- **SC-003**: Service startup with an existing database of up to 1,000 routes completes and begins serving traffic within 10 seconds.
- **SC-004**: 100% of create, update, and delete operations on proxy configuration produce a corresponding audit log entry with actor identity and timestamp.
- **SC-005**: Schema migrations complete successfully with no configuration data loss when upgrading from the previous schema version.
- **SC-006**: Administrators can retrieve the full change history for any individual ProxyHost through the audit log API, up to the configured retention period.
- **SC-007**: Audit log retention period defaults to 90 days and can be changed by an operator via configuration without requiring a code change or redeployment.

## Assumptions

- PostgreSQL is the target database, consistent with the existing `proxymanager-postgresql.container` Quadlet unit already defined in the deployment configuration.
- `proxysettings.{Environment}.json` transitions to a bootstrap/seed file: it seeds the database on first startup when the database is empty, then is ignored. The database is the sole live configuration source thereafter.
- `ProxyHost` is the canonical persisted abstraction. The system derives YARP-native routes and clusters from ProxyHost records; those derived objects are ephemeral and never stored directly.
- The existing in-memory `ConcurrentDictionary`-based store for `ProxyHost` records (from feature 001) will be replaced by database-backed persistence; an in-memory cache of the active translated configuration may be retained for routing performance but is no longer the system of record.
- The audit log's "actor identity" is the authenticated user identity from the JWT Bearer token on the management API request; system-initiated operations (startup load) are recorded as a system actor.
- Migrations are applied automatically at startup for this feature; an operator-run CLI migration path is out of scope but should not be architecturally prevented.
- Database connection configuration is provided via environment variables already supported by the Quadlet `.env` file mechanism.
- A UI for browsing audit logs is out of scope for this feature; only the API endpoint is in scope.

## Clarifications

### Session 2026-06-07

- Q: When an administrator creates or updates a proxy route via the API, does YARP need to start routing traffic immediately without restarting, or is restart-only acceptable? → A: Live reload required — changes take effect immediately, no restart needed.
- Q: What should happen to `proxysettings.{Environment}.json` once database persistence is in place? → A: Bootstrap/seed only — seeds the DB on first startup when empty; DB is the sole source thereafter.
- Q: How does `ProxyHost` (feature 001) relate to `ProxyRoute`/`ProxyCluster` in the new model? → A: ProxyHost is a high-level abstraction that simplifies YARP configuration; it is the persisted entity. The system derives YARP-native routes and clusters from ProxyHost records at runtime. Administrators work exclusively with ProxyHost.
- Q: Should a message be published to RabbitMQ when a ProxyHost is created, updated, or deleted? → A: Yes — publish a config-change event on every ProxyHost create/update/delete.
- Q: How long should audit log entries be retained? → A: 90 days default, configurable — operators can supply a different number of days via configuration without code changes.

## Dependencies

- PostgreSQL database instance (already provisioned in deployment as `proxymanager-postgresql.container`)
- RabbitMQ message bus (already in architecture) for publishing ProxyHost config-change events
- Management API authentication (JWT Bearer via Authentik) must be in place to populate actor identity in audit records
- Feature 001 (ProxyHost API) provides the existing in-memory model that this feature extends
