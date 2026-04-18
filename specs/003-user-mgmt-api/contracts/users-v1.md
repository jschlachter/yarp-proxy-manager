# API Contract: User Management — `/v1/users`

**Version**: v1  
**Base path**: `/v1/users`  
**Authentication**: JWT Bearer (Authentik-issued)  
**Authorization**: All endpoints require a valid JWT. Write endpoints additionally require `pm_role = "Admin"` claim.  
**Error format**: RFC 9457 `application/problem+json` for all error responses.

> **Revision note (2026-04-18)**: Added `nickname` and optional `profileImageUrl` to all user response and request bodies.

---

## Endpoints

### `GET /v1/users`

List authorized users. Returns active users by default.

**Authorization**: Any authenticated user (Admin or ReadOnly).

**Query parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `includeDeactivated` | bool | `false` | When `true`, includes deactivated users in results |
| `page` | int | `1` | 1-based page number |
| `pageSize` | int | `20` | Max 100 |

**Response 200 OK**:
```json
{
  "items": [
    {
      "sub": "abc123de-f456-7890-abcd-ef1234567890",
      "displayName": "Alice Anderson",
      "nickname": "Alice",
      "email": "alice@west94.org",
      "profileImageUrl": "https://auth.west94.io/application/o/proxy-manager/profile.jpg",
      "accessLevel": "Admin",
      "status": "Active",
      "createdAt": "2026-04-18T10:00:00Z",
      "lastModifiedAt": "2026-04-18T10:00:00Z",
      "deactivatedAt": null
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

**Response 401**: No or invalid token.

---

### `GET /v1/users/{sub}`

Get a single user by their Authentik subject identifier.

**Authorization**: Any authenticated user (Admin or ReadOnly).

**Path parameters**:

| Parameter | Type | Description |
|---|---|---|
| `sub` | string | Authentik subject claim value (opaque string) |

**Response 200 OK**:
```json
{
  "sub": "abc123de-f456-7890-abcd-ef1234567890",
  "displayName": "Alice Anderson",
  "nickname": "Alice",
  "email": "alice@west94.org",
  "profileImageUrl": "https://auth.west94.io/application/o/proxy-manager/profile.jpg",
  "accessLevel": "Admin",
  "status": "Active",
  "createdAt": "2026-04-18T10:00:00Z",
  "lastModifiedAt": "2026-04-18T10:00:00Z",
  "deactivatedAt": null
}
```

**Response 401**: No or invalid token.  
**Response 404**: No user with the given `sub` exists (active or deactivated).

---

### `POST /v1/users`

Create a new authorized user. If the `sub` matches a deactivated user, the existing record is reactivated.

**Authorization**: `pm_role = "Admin"` required.

**Request body** (`application/json`):
```json
{
  "sub": "abc123de-f456-7890-abcd-ef1234567890",
  "displayName": "Bob Builder",
  "nickname": "Bob",
  "email": "bob@west94.org",
  "profileImageUrl": null,
  "accessLevel": "ReadOnly"
}
```

**Fields**:

| Field | Type | Required | Description |
|---|---|---|---|
| `sub` | string | Yes | Authentik subject claim; treated as opaque |
| `displayName` | string | Yes | Full/formal name |
| `nickname` | string | Yes | Preferred short display name |
| `email` | string | Yes | Email address |
| `profileImageUrl` | string? | No | Absolute `http`/`https` URL to profile image; `null` if not provided |
| `accessLevel` | string | Yes | `"Admin"` or `"ReadOnly"` |

**Response 201 Created** (new user):
- `Location` header: `/v1/users/{sub}`
- Body: `AuthorizedUserDto`

**Response 200 OK** (reactivation of deactivated user):
- `X-User-Reactivated: true` response header
- Body: `AuthorizedUserDto` with `status: "Active"`

**Response 400**: Missing or invalid fields (including invalid `profileImageUrl` format).  
**Response 401**: No or invalid token.  
**Response 403**: Token present but `pm_role != "Admin"`.  
**Response 409**: User with this `sub` already exists and is active.

---

### `PATCH /v1/users/{sub}`

Update a user's access level. Only `accessLevel` may be changed via this endpoint.

**Authorization**: `pm_role = "Admin"` required.

**Path parameters**: `sub` — Authentik subject claim.

**Request body** (`application/json`):
```json
{
  "accessLevel": "Admin"
}
```

**Response 200 OK**: Updated `AuthorizedUserDto`.  
**Response 400**: Invalid `accessLevel` value.  
**Response 401**: No or invalid token.  
**Response 403**: `pm_role != "Admin"`.  
**Response 404**: No active user with the given `sub`.

---

### `DELETE /v1/users/{sub}`

Deactivate a user (soft delete). The record is retained; the user's access is revoked immediately.

**Authorization**: `pm_role = "Admin"` required.

**Path parameters**: `sub` — Authentik subject claim.

**Response 204 No Content**: User successfully deactivated.  
**Response 401**: No or invalid token.  
**Response 403**: `pm_role != "Admin"`.  
**Response 404**: No active user with the given `sub`.

---

### `GET /v1/users/audit`

Query the user audit log.

**Authorization**: Any authenticated user (Admin or ReadOnly).

**Query parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `sub` | string | (none) | Filter entries for a specific user `sub` |
| `from` | ISO 8601 datetime | (none) | Earliest `occurredAt` to include |
| `to` | ISO 8601 datetime | (none) | Latest `occurredAt` to include |
| `page` | int | `1` | 1-based page number |
| `pageSize` | int | `20` | Max 100 |

**Response 200 OK**:
```json
{
  "items": [
    {
      "id": "11111111-2222-3333-4444-555555555555",
      "subjectSub": "abc123de-f456-7890-abcd-ef1234567890",
      "operation": "Created",
      "previousAccessLevel": null,
      "newAccessLevel": "ReadOnly",
      "actorSub": "xyz999aa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "occurredAt": "2026-04-18T10:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

**Response 401**: No or invalid token.

---

## Claim Contract (Authentik Configuration)

The JWT issued by Authentik for Proxy Manager API clients MUST include:

```json
{
  "sub": "<user-uuid>",
  "pm_role": "Admin"   // or "ReadOnly"
}
```

Tokens without `pm_role` are treated as unauthorized for write operations (403 response). Authentik application configuration must map the appropriate user attribute or group membership to the `pm_role` claim.

---

## Authorization Policy Registration

```csharp
// In ServiceCollectionExtensions.AddProxyManagerServices():
services.AddAuthorization(options =>
{
    options.AddPolicy("UserAdmin", policy =>
        policy.RequireClaim("pm_role", "Admin"));
});
```
