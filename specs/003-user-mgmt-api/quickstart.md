# Quickstart: User Management API (003-user-mgmt-api)

## Prerequisites

- ProxyManager.API running locally (`dotnet run --project src/ProxyManager.API/ProxyManager.API.csproj`)
- A valid JWT from Authentik with `pm_role` claim set to `"Admin"` or `"ReadOnly"`
- `curl` or Scalar UI at `https://localhost:5001/scalar`

## Authentik Token Setup

Your Authentik application token must include the `pm_role` custom claim. Configure a Property Mapping in Authentik:

```
Expression: return "Admin"   # or map from group membership
Scope name: pm_role
```

Alternatively, for local development use the test JWT factory in integration tests (see `TestJwtFactory.cs`).

---

## Example Requests

### List users

```bash
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/v1/users
```

### List users (including deactivated)

```bash
curl -H "Authorization: Bearer $TOKEN" "https://localhost:5001/v1/users?includeDeactivated=true"
```

### Get a specific user

```bash
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/v1/users/abc123de-f456-7890-abcd-ef1234567890
```

### Add a user (Admin token required)

```bash
curl -X POST https://localhost:5001/v1/users \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sub": "abc123de-f456-7890-abcd-ef1234567890",
    "displayName": "Bob Builder",
    "nickname": "Bob",
    "email": "bob@west94.org",
    "profileImageUrl": null,
    "accessLevel": "ReadOnly"
  }'
```

### Add a user with a profile image

```bash
curl -X POST https://localhost:5001/v1/users \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sub": "def456gh-1234-5678-ijkl-mn9012345678",
    "displayName": "Carol Casey",
    "nickname": "Carol",
    "email": "carol@west94.org",
    "profileImageUrl": "https://auth.west94.io/application/o/proxy-manager/carol.jpg",
    "accessLevel": "Admin"
  }'
```

### Update access level (Admin token required)

```bash
curl -X PATCH https://localhost:5001/v1/users/abc123de-f456-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"accessLevel": "Admin"}'
```

### Deactivate a user (Admin token required)

```bash
curl -X DELETE https://localhost:5001/v1/users/abc123de-f456-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer $TOKEN"
```

### Query audit log

```bash
# All entries
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/v1/users/audit

# Filter by user and date range
curl -H "Authorization: Bearer $TOKEN" \
  "https://localhost:5001/v1/users/audit?sub=abc123de-f456-7890-abcd-ef1234567890&from=2026-04-01T00:00:00Z"
```

---

## Running Tests

```bash
# All tests
dotnet test

# Integration tests only
dotnet test tests/ProxyManager.API.Tests --filter Category=Integration

# Unit tests only
dotnet test --filter Category=Unit

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `401 Unauthorized` on all requests | Token expired or signed with wrong key |
| `403 Forbidden` on write operations | Token missing `pm_role` claim or value is not `"Admin"` |
| `400 Bad Request` with profileImageUrl error | URL is not an absolute `http`/`https` URL |
| `409 Conflict` on POST | User with that `sub` is already active — use PATCH to update |
| `200 OK` (not 201) on POST with `X-User-Reactivated: true` | User existed as deactivated and was reactivated |
| `404 Not Found` on DELETE | User is already deactivated or never existed |
