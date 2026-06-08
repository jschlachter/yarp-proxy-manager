# Feature Specification: User Management API

**Feature Branch**: `003-user-mgmt-api`  
**Created**: 2026-04-18  
**Status**: Draft  
**Input**: User description: "Create a new REST API for querying and managing users authorized users of Proxy Manager. The API should be accessible only to authorized clients and any changes to users or their access levels needs to be logged appropriately"

## Clarifications

### Session 2026-04-18

- Q: When a user is deleted via the API, how should the system treat that record? → A: Soft delete — mark as deactivated/revoked, keep record and full history
- Q: What are the specific access levels (roles) the system should support? → A: Two roles: Admin (full CRUD) and ReadOnly (query only)
- Q: How are users identified in the API? → A: Authentik subject claim (`sub`) — stable opaque UUID from the identity provider
- Q: If a previously deactivated user is added again via the create endpoint, what should happen? → A: Reactivate the existing record and update the access level, logged as a reactivation in the audit log
- Q: Should this API include versioning in its URL structure? → A: Yes — version from the start (e.g., `/v1/users`, `/v1/users/audit`)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Query Authorized Users (Priority: P1)

An administrator needs to retrieve a list of all users who are authorized to access Proxy Manager, including their current access levels. This is the foundational read operation that enables auditing and management workflows.

**Why this priority**: Without the ability to view existing users and their permissions, no meaningful management can occur. This is the baseline for all other operations.

**Independent Test**: Can be fully tested by authenticating as an admin client and retrieving the user list — delivers visibility into who has access to the system.

**Acceptance Scenarios**:

1. **Given** a valid authorized client credential, **When** a GET request is made to the users endpoint, **Then** the response contains a list of all authorized users with their access levels and identifiers
2. **Given** an unauthorized or unauthenticated client, **When** a GET request is made to the users endpoint, **Then** the response returns an access-denied error and no user data is revealed
3. **Given** a valid authorized client credential, **When** a GET request is made for a specific user by identifier, **Then** the response contains only that user's details

---

### User Story 2 - Add or Update Authorized Users (Priority: P2)

An administrator needs to grant access to a new user or update an existing user's access level within Proxy Manager.

**Why this priority**: The ability to onboard new users and change access levels is the primary mutation operation and enables delegation of Proxy Manager capabilities.

**Independent Test**: Can be tested by adding a new user and verifying they appear in the user list with the correct access level.

**Acceptance Scenarios**:

1. **Given** a valid authorized admin client and a new user's details, **When** a POST request is made to create the user, **Then** the user is added with the specified access level and a success response is returned
2. **Given** a valid authorized admin client and an existing user, **When** a PUT/PATCH request is made to change that user's access level, **Then** the user's access level is updated and the change is recorded in the audit log
3. **Given** a valid authorized admin client and an active user's identifier, **When** a POST request is made to create that user again, **Then** the response returns a conflict error and no duplicate is created
4. **Given** a valid authorized admin client and a previously deactivated user's identifier, **When** a POST request is made to create that user, **Then** the user is reactivated with the specified access level and a reactivation entry is written to the audit log

---

### User Story 3 - Revoke User Access (Priority: P3)

An administrator needs to remove a user's authorization from Proxy Manager, immediately preventing that user from accessing the system.

**Why this priority**: Access revocation is a critical security operation but less frequent than querying or updating, and depends on user creation being in place.

**Independent Test**: Can be tested by removing a user and verifying they no longer appear as authorized and cannot perform actions.

**Acceptance Scenarios**:

1. **Given** a valid authorized admin client and an active user, **When** a DELETE request is made for that user, **Then** the user is marked as deactivated (not removed), the action is recorded in the audit log, and the user no longer appears in the default active-users list
2. **Given** a valid authorized admin client and a non-existent user identifier, **When** a DELETE request is made, **Then** the response returns a not-found error
3. **Given** an unauthorized client, **When** a DELETE request is made for any user, **Then** the response returns an access-denied error and no user is deactivated

---

### User Story 4 - Review Audit Log of User Changes (Priority: P4)

An administrator needs to review a chronological log of all changes made to user accounts and access levels for compliance and accountability purposes.

**Why this priority**: Audit log querying is secondary to the management operations themselves but is required for compliance and troubleshooting.

**Independent Test**: Can be tested by making user changes and then querying the audit log to verify entries were recorded.

**Acceptance Scenarios**:

1. **Given** a valid authorized admin client, **When** a GET request is made to the audit log endpoint, **Then** a chronological list of all user-related changes is returned, including who made the change, when, and what was changed
2. **Given** a valid authorized admin client, **When** filtering the audit log by user identifier or date range, **Then** only matching entries are returned
3. **Given** an unauthorized client, **When** a GET request is made to the audit log endpoint, **Then** the response returns an access-denied error

---

### Edge Cases

- What happens when a request is made with a valid credential but insufficient access level (e.g., a read-only role attempting a delete)?
- How does the system handle user identifiers that contain special characters or exceed expected length?
- What happens if an audit log write fails — does the primary operation roll back or continue?
- What happens when the same user identifier is submitted for creation twice in rapid succession (race condition)?
- How does the system behave when the authorized user list is empty?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose versioned endpoints under `/v1/` to list all authorized users (active only by default, with an option to include deactivated), retrieve a single user by identifier, create a user, update a user's access level, and soft-delete (deactivate) a user
- **FR-002**: System MUST require all API clients to authenticate using JWT Bearer tokens issued by the existing Authentik identity provider (same mechanism as ProxyManager.API)
- **FR-003**: System MUST enforce two access levels: **Admin** clients may perform all create, update, and deactivate operations; **ReadOnly** clients may only perform read operations (list users, get user, query audit log). Requests from ReadOnly clients attempting write operations MUST be rejected with an authorization error.
- **FR-004**: System MUST write an audit log entry for every create, update, and delete operation, capturing: affected user identifier, type of change, previous access level (for updates), new access level, acting client identity, and timestamp
- **FR-005**: System MUST return structured error responses for authentication failures, authorization failures, not-found conditions, and validation errors without leaking internal system details
- **FR-006**: System MUST validate that required fields (Authentik `sub` claim as user identifier, access level) are present and non-empty for all write operations; the `sub` value MUST be treated as an opaque string and not parsed or reformatted
- **FR-007**: System MUST prevent duplicate active user records for the same `sub` identifier. If a create request targets an already-active user, return a conflict error. If the targeted user is deactivated, the system MUST reactivate that record with the specified access level and log the event as a reactivation (not a new creation) in the audit log.
- **FR-008**: System MUST expose a versioned endpoint at `/v1/users/audit` to query the audit log, supporting optional filtering by user identifier and date range
- **FR-009**: System MUST ensure that a failed audit log write causes the corresponding user operation to be rolled back or the failure is surfaced to the caller — silent audit failures are not acceptable

### Key Entities

- **AuthorizedUser**: Represents an entity permitted to access Proxy Manager. Primary identifier is the Authentik **subject claim (`sub`)** — a stable, opaque UUID that does not change if the user's email or display name changes. Additional attributes: full name (DisplayName), nickname, email address, optional profile image URL, assigned access level (Admin or ReadOnly), status (active/deactivated), creation timestamp, last modified timestamp, deactivated timestamp (nullable).
- **AccessLevel**: Represents the permission tier assigned to a user. Two values: **Admin** (full create, update, deactivate, and audit log access) and **ReadOnly** (query users and audit log only). Determines which API operations the user may perform within Proxy Manager.
- **UserAuditEntry**: Represents a record of a change to an AuthorizedUser. Attributes: entry identifier, affected user identifier, change type (created/updated/deactivated/reactivated), previous access level, new access level, acting client identity, timestamp.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authorized administrator can complete the full user lifecycle (add, view, update, revoke) via the API in under 2 minutes
- **SC-002**: Every create, update, and delete operation produces a corresponding audit log entry — 100% audit coverage with no silent failures
- **SC-003**: Unauthorized or unauthenticated requests are rejected 100% of the time with no user data or audit data exposed in the response
- **SC-004**: Administrators can retrieve and filter the audit log by user identifier and date range, enabling reconstruction of any user's access history
- **SC-005**: The API handles at least 50 concurrent management requests without errors or data corruption

## Assumptions

- Users in this context are operators or service accounts managed in Authentik (the existing OIDC provider). This API maintains authorization records mapping Authentik `sub` claims to Proxy Manager access levels — it does not replace or replicate the identity provider.
- The API will be hosted as a new versioned endpoint group (`/v1/`) within the existing ProxyManager.API project, reusing the JWT Bearer authentication already in place. Future breaking changes will increment the version prefix rather than modify existing endpoints.
- Access levels are a fixed two-value enumeration: **Admin** and **ReadOnly**. No additional roles will be introduced in this feature.
- Audit log entries are stored persistently given the compliance intent. The storage mechanism is to be determined during planning.
- The acting client identity recorded in audit log entries is derived from the JWT claims of the authenticated request.
