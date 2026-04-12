# Data Model: Proxy Route Manager Application

**Feature**: `002-proxy-route-manager` | **Date**: 2026-04-05

## Overview

This application has no local database. All persistent data (routing configuration, maintainer assignments, audit log) is owned and stored by the ProxyManager API. User identity is not stored locally — it is read at request time from headers forwarded by ProxyManager.

The UI displays routing data exactly as returned by the ProxyManager API. ProxyManager is responsible for interpreting that data and producing its internal routing configuration (YARP routes, clusters, transforms, etc.). The UI has no knowledge of or dependency on YARP's internal structure.

---

## Data Ownership

| Data | Owner | Notes |
|---|---|---|
| Routing configuration | ProxyManager API | Fetched live and displayed as-is |
| Maintainer assignments | ProxyManager API | API endpoints not yet implemented; UI will surface when available |
| Audit log | ProxyManager API | API endpoints not yet implemented; UI will surface when available |
| User identity | ProxyManager (via forwarded headers) | Read from `X-Auth-Sub` and `X-Auth-Groups` on each request; not persisted |

---

## Runtime Types (from ProxyManager API)

Defined in `src/ProxyManager.UI/types/index.ts`. These shapes mirror the ProxyManager API response contract. Field names follow the API's domain model — not YARP's internal structure.

### `UserSession`

Constructed server-side from forwarded request headers. Not persisted.

```typescript
interface UserSession {
  userId: string      // From X-Auth-Sub header (Authentik sub claim)
  groups: string[]    // From X-Auth-Groups header (comma-separated)
  isAdmin: boolean    // Derived: groups.includes(process.env.ADMIN_GROUP_CLAIM)
  accessToken: string // From Authorization header (stripped of "Bearer " prefix)
}
```

### `ProxyHost`

Represents a single routing configuration entry as returned by the ProxyManager API. The exact field set is defined by the API contract; the UI renders what it receives.

```typescript
interface ProxyHost {
  id: string               // Unique identifier
  name: string             // Human-readable name
  upstreamUrl: string      // Destination for proxied traffic
  hostnames: string[]      // Hostnames this entry responds to
  pathPrefix?: string      // Optional path prefix match
  isEnabled: boolean       // Whether this entry is active
  createdAt: string        // ISO 8601
  updatedAt: string        // ISO 8601
}
```

> **Note**: The ProxyManager API translates `ProxyHost` records into YARP routes and clusters at runtime. The UI neither produces nor consumes YARP configuration directly.

---

## Future Types (deferred — pending ProxyManager API implementation)

These shapes will be added to `types/index.ts` when the corresponding ProxyManager API endpoints are implemented.

### `MaintainerAssignment` *(future)*

```typescript
interface MaintainerAssignment {
  proxyHostId: string
  userId: string
  userName: string
  assignedBy: string
  assignedAt: string  // ISO 8601
}
```

### `AuditEntry` *(future)*

```typescript
interface AuditEntry {
  id: string
  occurredAt: string  // ISO 8601
  actorId: string
  actorName: string
  action: 'host.create' | 'host.update' | 'host.delete' | 'maintainer.assign' | 'maintainer.remove'
  proxyHostId: string
  proxyHostName: string
  detail: Record<string, unknown> | null
}
```
