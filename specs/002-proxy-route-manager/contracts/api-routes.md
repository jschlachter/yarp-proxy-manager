# API Contracts: Proxy Route Manager UI

**Feature**: `002-proxy-route-manager` | **Date**: 2026-04-05

## Overview

The Next.js application exposes Route Handlers under `/api/` that act as a Backend-for-Frontend (BFF) layer, delegating to the ProxyManager API. Authentication is handled entirely by ProxyManager (YARP) before any request reaches this application. The UI reads the authenticated user's identity and access token from headers forwarded by ProxyManager on every request.

All endpoints that receive requests without an `X-Auth-Sub` header return `401` (this is a defense-in-depth check — ProxyManager should prevent unauthenticated requests from reaching the container). Authorization failures return `403`. All error responses use RFC 9457 Problem Details (`Content-Type: application/problem+json`).

---

## Authentication

**No auth endpoints exist in this application.** Login, logout, and session management are handled by ProxyManager's OIDC integration with Authentik. Users who are not authenticated are redirected by ProxyManager before reaching the Next.js container.

Forwarded headers available to all server-side code:

| Header | Content |
|---|---|
| `X-Auth-Sub` | Authentik `sub` claim — unique user identifier |
| `X-Auth-Groups` | Comma-separated list of the user's Authentik groups |
| `Authorization` | `Bearer <access_token>` — forwarded to ProxyManager.API calls |

---

## Routes

### List Routes

```
GET /api/routes
```

Fetches all routes from ProxyManager API.

**Authorization**: Any authenticated user

**Query parameters**:
- `page` (integer, default: 1)
- `pageSize` (integer, default: 50, max: 50)

**Response `200`**:
```json
{
  "routes": [
    {
      "routeId": "string",
      "clusterId": "string",
      "match": { "hosts": ["string"], "path": "string" },
      "transforms": []
    }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 0
}
```

---

### Create Route

```
POST /api/routes
```

**Authorization**: Administrator only (`X-Auth-Groups` must contain the admin group)

**Request body**:
```json
{
  "routeId": "string (required, unique, kebab-case)",
  "clusterId": "string (required)",
  "match": {
    "hosts": ["string"],
    "path": "string"
  },
  "transforms": []
}
```

**Response `201`**: Created route object

**Response `409`** (Problem Details): Route ID already exists

**Response `422`** (Problem Details): Validation failure

---

### Get Route

```
GET /api/routes/{id}
```

**Authorization**: Any authenticated user

**Response `200`**: Full route object

**Response `404`** (Problem Details): Route not found

---

### Update Route

```
PUT /api/routes/{id}
```

**Authorization**: Administrator or assigned maintainer (enforced by ProxyManager API)

**Request body**: Same shape as create (all fields required)

**Response `200`**: Updated route object

**Response `403`** (Problem Details): Insufficient permissions

**Response `404`** (Problem Details): Route not found

---

### Delete Route

```
DELETE /api/routes/{id}
```

**Authorization**: Administrator only (enforced by ProxyManager API)

**Response `204`**: Deleted

**Response `403`** (Problem Details): Insufficient permissions

**Response `404`** (Problem Details): Route not found

---

## Deferred Endpoints (pending ProxyManager API implementation)

The following operations are owned by the ProxyManager API. No BFF routes will be added for these until the ProxyManager API contracts are defined. When available, methods are added to `lib/proxy-manager-client.ts` and new BFF handlers are added under `app/api/`.

| Operation | Future BFF Route | Delegated To |
|---|---|---|
| List maintainers for a route | `GET /api/routes/{id}/maintainers` | ProxyManager API |
| Assign maintainer | `POST /api/routes/{id}/maintainers` | ProxyManager API |
| Remove maintainer | `DELETE /api/routes/{id}/maintainers/{userId}` | ProxyManager API |
| View audit log | `GET /api/audit` | ProxyManager API |

---

## Error Response Format (RFC 9457)

All error responses:

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Human-readable error title",
  "status": 403,
  "detail": "Specific reason for this instance of the error"
}
```

`Content-Type: application/problem+json`
