# Feature Specification: Proxy Route Manager Application

**Feature Branch**: `002-proxy-route-manager`
**Created**: 2026-04-05
**Status**: Draft
**Input**: User description: "Build an application that can help me manage routing information for applications hosted in ProxyManager. The app should be modular allowing me to easily add new features as they're built out. Additionally provide a way to designate select individuals responsible for maintaining this information."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View and Manage Proxy Routes (Priority: P1)

An administrator opens the Proxy Route Manager and sees a unified list of all routing configurations for applications hosted in ProxyManager. They can add a new route, edit an existing one, or remove a route that is no longer needed.

**Why this priority**: Core functionality — without the ability to view and manage routes, the application delivers no value. All other features build on top of this foundation.

**Independent Test**: Can be fully tested by navigating to the routes list, creating a new route entry, editing it, and deleting it — and verifying each change is reflected in the list and persisted across sessions.

**Acceptance Scenarios**:

1. **Given** the user is authenticated, **When** they open the application, **Then** they see a list of all currently configured proxy routes including the route name, upstream destination, and current status.
2. **Given** the routes list is displayed, **When** the user selects "Add Route" and fills in the required fields, **Then** the new route appears in the list and is available for editing or deletion.
3. **Given** an existing route is in the list, **When** the user selects it and modifies a field, **Then** the change is saved and the updated values are displayed.
4. **Given** an existing route is in the list, **When** the user deletes it, **Then** it is removed from the list and no longer present on subsequent loads.

---

### User Story 2 - Assign and Manage Route Maintainers (Priority: P2)

An administrator designates specific individuals as maintainers for one or more proxy routes. A maintainer can update the routing information for routes they are assigned to, but cannot delete routes or manage other maintainers.

**Why this priority**: Delegating ownership of specific routes to responsible parties is a key requirement. Without this, all changes must go through an administrator, creating a bottleneck.

**Independent Test**: Can be fully tested by assigning a user as a maintainer for a route, logging in as that user, verifying they can edit the route, and verifying they cannot delete it or assign other maintainers.

**Acceptance Scenarios**:

1. **Given** a route exists, **When** an administrator assigns a person as its maintainer, **Then** that person appears in the route's maintainer list.
2. **Given** a user is a maintainer for a route, **When** they log in and navigate to that route, **Then** they can update its configuration fields.
3. **Given** a user is a maintainer for a route, **When** they attempt to delete the route or manage its maintainer list, **Then** the action is denied and an appropriate message is displayed.
4. **Given** a user is not a maintainer for a route, **When** they view that route, **Then** the configuration fields are read-only.
5. **Given** a maintainer is assigned to a route, **When** an administrator removes them, **Then** they lose edit access to that route.

---

### User Story 3 - Modular Feature Integration (Priority: P3)

A developer adds a new management module (e.g., health check configuration, TLS certificate tracking) to the application without modifying existing route management or maintainer functionality.

**Why this priority**: The long-term value of the application depends on its ability to grow. This story ensures new capabilities can be added as standalone modules rather than requiring a rebuild of existing features.

**Independent Test**: Can be fully tested by adding a new module to the application, verifying it appears in the navigation alongside existing modules, and confirming all existing modules continue to function correctly.

**Acceptance Scenarios**:

1. **Given** the application is running with existing modules, **When** a new module is enabled, **Then** it appears in the application navigation and is accessible without affecting existing modules.
2. **Given** a module is disabled or not yet built, **When** a user navigates the application, **Then** they do not see incomplete or broken navigation entries for that module.
3. **Given** a new module is added, **When** an existing module is used, **Then** its behavior is unchanged.

---

### Edge Cases

- What happens when a user attempts to create a route with an upstream destination that is already assigned to another route?
- When a maintainer's Authentik account is deactivated, their route assignments are preserved. Since Authentik controls authentication, a deactivated user cannot log in and therefore cannot exercise any access. Assignments are automatically effective again if the account is reinstated.
- What happens when a route's required fields are left blank during creation or editing?
- When two users edit the same route simultaneously, the last save wins with no conflict notification; this is acceptable given the small expected user base.
- What is displayed when no routes have been configured yet (empty state)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow authenticated administrators to create, view, edit, and delete proxy route entries.
- **FR-000**: System MUST retrieve routing configuration entries from the ProxyManager API and display them as returned by the API. ProxyManager is responsible for translating this data into its internal routing configuration; the UI presents the domain data only.
- **FR-002**: System MUST display all routes in a consolidated list with key attributes (name, upstream destination, status) visible at a glance.
- **FR-003**: System MUST allow administrators to assign one or more maintainers to each route.
- **FR-004**: System MUST allow administrators to remove maintainers from a route.
- **FR-005**: System MUST restrict maintainers to editing only the routes they are assigned to; they cannot delete routes or manage maintainer assignments.
- **FR-006**: System MUST present routing configuration fields as read-only to users who are neither administrators nor assigned maintainers.
- **FR-007**: System MUST validate required fields before saving any route or maintainer change, and display clear error messages for invalid input.
- **FR-008**: System MUST persist all route and maintainer changes so they are available on subsequent sessions.
- **FR-009**: System MUST support a modular structure allowing new feature modules to be added independently without disrupting existing modules.
- **FR-010**: System MUST provide navigation that reflects only currently enabled modules.
- **FR-011**: System MUST display the list of assigned maintainers on each route's detail view.
- **FR-012**: System MUST log changes to route configurations, capturing who made the change and when.

### Key Entities

- **Proxy Route**: Represents a single routing configuration entry as returned by the ProxyManager API. Key attributes: name, upstream destination, path or host match pattern, status (active/inactive), assigned maintainers, creation date, last modified date. ProxyManager uses this data to produce its internal routing configuration; the UI does not interpret or transform it.
- **Maintainer**: A person designated as responsible for one or more routes. Key attributes: identity (name/account reference), assigned routes, role scope (edit-only for their routes).
- **Administrator**: A user with full management privileges — can manage all routes and all maintainer assignments. Administrator status is conveyed by an Authentik group membership or OIDC claim in the user's token; no separate in-app role assignment is needed.
- **Module**: A discrete feature area of the application (e.g., route management, maintainer management). Can be enabled or disabled independently. Contains its own navigation entry and functionality.
- **Audit Entry**: A record of a change event, capturing the actor, the affected route, the type of change, and the timestamp.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Administrators can view all configured routes and create or modify a route in under 2 minutes from login.
- **SC-002**: Maintainer assignments take effect immediately — a newly assigned maintainer can edit their route without any delay after assignment.
- **SC-003**: 100% of route configuration changes are recorded in the audit log with actor identity and timestamp.
- **SC-004**: A new application module can be integrated and appear in navigation without modifying any existing module's code or configuration.
- **SC-005**: Users without appropriate access are prevented from making unauthorized changes in 100% of attempts.
- **SC-006**: All required field validations are surfaced to the user before data is submitted, resulting in zero silent data loss on invalid input.

## Clarifications

### Session 2026-04-05

- Q: How is the "administrator" role determined? → A: Admin role comes from a claim or group in Authentik (e.g., a group membership synced via OIDC token)
- Q: What is the relationship between this app and ProxyManager's routing config? → A: App reads from ProxyManager API and displays data as-is; ProxyManager uses that data to produce its internal routing configuration
- Q: What is the application delivery format? → A: Web UI — browser-based interface
- Q: How should concurrent edit conflicts be handled? → A: Last write wins — most recent save overwrites without conflict notification
- Q: What happens to maintainer assignments when a user's account is deactivated? → A: Assignments are preserved; access is gated entirely by Authentik authentication — a deactivated user cannot log in regardless of their assignments

## Assumptions

- This application is delivered as a browser-based web UI; no client installation is required for administrators or maintainers.
- Authentication and identity management are provided by the existing system (Authentik via OIDC); the application does not manage passwords or user accounts directly.
- Role distinction is binary at launch: administrators have full access; all other authenticated users are potential maintainers with route-scoped edit access only.
- Administrator status is determined by an Authentik group membership or OIDC claim present in the user's token — no in-app admin designation is required.
- The list of users who can be assigned as maintainers is drawn from the existing authenticated user pool — the application does not create or invite new user accounts.
- Route data originates from the ProxyManager API and is displayed as returned. ProxyManager is responsible for translating its stored configuration into routing rules; the UI does not interpret or reformat that data.
- A route may have zero or more maintainers; having no maintainer is valid (editable only by administrators).

## External Dependencies

- **ProxyManager API**: The authoritative source of routing configuration. This application reads routing entries from the API and displays them as-is. Write operations (create, edit, delete) are submitted back to the ProxyManager API, which applies the changes to its internal routing configuration. Failure or unavailability of the API must result in a clear user-facing error; the application must not cache stale data silently.
- **Authentik (OIDC)**: Provides user identity and the administrator role claim. Required for authentication and access control decisions.

## Out of Scope

- Real-time health monitoring or alerting for proxied routes
- Automated deployment or propagation of route changes to ProxyManager infrastructure
- TLS certificate management (potential future module)
- Multi-tenancy or organizational hierarchy beyond the administrator/maintainer distinction
