# Feature Specification: Proxy Host Management API

**Feature Branch**: `001-proxyhost-api`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Create a new REST API for querying and managing proxyhosts, the API will
only be accessible to authorized clients. Any changes to proxyhosts need to be logged appropriately"

## Assumptions

- "Authorized clients" means clients holding a valid JWT bearer token issued by the existing
  Authentik OIDC authority. Anonymous or unauthenticated requests are rejected at the API boundary.
- "Proxyhosts" are routing configuration entries that map an incoming public hostname (and optional
  path prefix) to an upstream backend service address.
- Logging changes means writing structured audit log entries that capture who made a change, what
  the previous and new state was, and when the change occurred. Audit logs are append-only.
- Listing proxyhosts returns paginated results ordered by hostname ascending.
- Deletion is permanent (no soft-delete). A proxyhost actively routing traffic can still be deleted;
  the caller is responsible for verifying impact before deletion.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Query Proxy Hosts (Priority: P1)

An authorized administrator opens a management client and needs to see all configured proxy hosts to
understand the current routing table. They can list all hosts and inspect any individual host's
full configuration.

**Why this priority**: Reading the proxy host list is the entry point for every other management
action. Without it, operators cannot discover what exists before making changes.

**Independent Test**: Can be fully tested by authenticating with a valid token and performing read
requests. No write operations required. Delivers a read-only view of the routing table as the MVP.

**Acceptance Scenarios**:

1. **Given** a client with a valid token, **When** they request the proxy host list, **Then** they
   receive a paginated collection of all proxy hosts including hostname, upstream address, and status.
2. **Given** a client with a valid token, **When** they request a specific proxy host by its unique
   identifier, **Then** they receive the full configuration for that host.
3. **Given** a client without a valid token, **When** they request the host list or a specific host,
   **Then** the request is denied with an authorization error and no data is returned.
4. **Given** a valid request for a proxy host ID that does not exist, **When** the request is
   processed, **Then** a clear "not found" response is returned.

---

### User Story 2 - Create a Proxy Host (Priority: P2)

An authorized administrator needs to add a new service to the proxy by creating a proxy host entry
that maps a public hostname to an upstream backend address.

**Why this priority**: Creation is the primary write operation; update and delete depend on hosts
already existing.

**Independent Test**: Can be verified by creating a proxy host with valid data and confirming it
appears in the list and is retrievable by ID. No prior proxy hosts are required.

**Acceptance Scenarios**:

1. **Given** a client with a valid token and valid new host data, **When** they submit the create
   request, **Then** the new proxy host is persisted and its complete configuration is returned.
2. **Given** a proxy host with the same hostname already exists, **When** a create request for that
   hostname is submitted, **Then** the request is rejected with a clear conflict error.
3. **Given** a create request with missing required fields (e.g., no upstream address), **When**
   processed, **Then** structured validation errors are returned identifying each missing field.
4. **Given** a successful create, **When** the audit log is queried, **Then** an entry recording the
   creation — including actor identity, new configuration, and UTC timestamp — is present.

---

### User Story 3 - Update a Proxy Host (Priority: P3)

An authorized administrator needs to modify an existing proxy host, such as changing its upstream
address, updating a path prefix rule, or toggling its active status.

**Why this priority**: Updates are an operational necessity but depend on hosts already existing.
Query and create are prerequisites.

**Independent Test**: Can be verified by creating a proxy host, updating it, then retrieving it to
confirm the changes are reflected.

**Acceptance Scenarios**:

1. **Given** a client with a valid token and an existing proxy host, **When** they submit a valid
   update payload, **Then** the proxy host is updated and the new configuration is returned.
2. **Given** an update request for a proxy host ID that does not exist, **When** processed, **Then**
   a "not found" response is returned and no state is changed.
3. **Given** an update request with invalid data (e.g., malformed upstream address), **When**
   processed, **Then** structured validation errors are returned and the existing configuration
   is unchanged.
4. **Given** a successful update, **When** the audit log is queried, **Then** an entry recording the
   update — including actor identity, previous state, new state, and UTC timestamp — is present.

---

### User Story 4 - Delete a Proxy Host (Priority: P4)

An authorized administrator needs to permanently remove a proxy host when a service is decommissioned
or a routing rule is no longer needed.

**Why this priority**: Deletion is the least frequently used operation and carries the most
operational risk; it is lower priority than querying, creating, and updating.

**Independent Test**: Can be verified by creating a proxy host, deleting it, then confirming it no
longer appears in the list or is retrievable by ID.

**Acceptance Scenarios**:

1. **Given** a client with a valid token and an existing proxy host, **When** they submit a delete
   request, **Then** the proxy host is permanently removed.
2. **Given** a delete request for a proxy host ID that does not exist, **When** processed, **Then**
   a "not found" response is returned and no state is changed.
3. **Given** a successful deletion, **When** the audit log is queried, **Then** an entry recording
   the deletion — including actor identity, the deleted configuration, and UTC timestamp — is present.
4. **Given** a client without a valid token, **When** they submit a delete request, **Then** the
   request is denied and nothing is deleted.

---

### Edge Cases

- What happens when the upstream address is unreachable at the time of creation or update? The API
  accepts the configuration regardless — upstream reachability is a runtime concern, not a
  validation concern at write time.
- What happens when a list request returns zero results? An empty paginated collection is returned;
  this is not an error condition.
- What happens when a caller provides unrecognized fields in a create or update payload? Extra
  fields are silently ignored; only known fields are processed.
- What happens when two callers attempt to create a proxy host with the same hostname simultaneously?
  Exactly one succeeds; the other receives a conflict error.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow authorized clients to retrieve a paginated list of all proxy hosts.
- **FR-002**: System MUST allow authorized clients to retrieve the full configuration of a single
  proxy host by its unique identifier.
- **FR-003**: System MUST allow authorized clients to create a new proxy host providing at minimum
  a public hostname and an upstream backend address.
- **FR-004**: System MUST reject create requests where the public hostname already exists, returning
  a conflict error.
- **FR-005**: System MUST allow authorized clients to update any field of an existing proxy host.
- **FR-006**: System MUST allow authorized clients to permanently delete an existing proxy host.
- **FR-007**: System MUST deny all requests — read and write — from clients without a valid
  authorization token, returning a clear authorization error.
- **FR-008**: System MUST record an audit log entry for every create, update, and delete operation.
- **FR-009**: Each audit log entry MUST capture: the identity of the actor, the operation type,
  the complete previous state (for updates and deletes), the complete new state (for creates and
  updates), and the UTC timestamp of the operation.
- **FR-010**: System MUST validate all create and update payloads and return structured error
  messages identifying each invalid or missing field.
- **FR-011**: Proxy host list responses MUST support pagination with a configurable page size.

### Key Entities

- **ProxyHost**: A routing configuration entry. Key attributes: unique identifier, public hostname,
  upstream backend address, optional path prefix, active/inactive status, created-at timestamp,
  updated-at timestamp.
- **AuditLogEntry**: An immutable record of a configuration change. Key attributes: entry identifier,
  actor identity, operation type (create/update/delete), snapshot of previous state, snapshot of
  new state, UTC timestamp.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Authorized administrators can list, create, update, and delete proxy hosts without
  requiring direct database access or technical assistance.
- **SC-002**: 100% of requests from clients without a valid authorization token are denied before
  any data is read or modified.
- **SC-003**: Every configuration change (create, update, delete) produces a corresponding audit log
  entry traceable to a specific actor within 1 second of the operation completing.
- **SC-004**: A paginated list of up to 500 proxy hosts is returned to an authorized caller within
  1 second under normal operating conditions.
- **SC-005**: Audit log entries are retained and queryable for a minimum of 90 days.
- **SC-006**: Validation errors for malformed or incomplete requests are returned in a consistent,
  human-readable format that identifies every invalid field without exposing system internals.
