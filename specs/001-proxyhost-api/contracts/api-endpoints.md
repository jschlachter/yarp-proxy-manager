# API Contract: Proxy Host Endpoints

**Branch**: `001-proxyhost-api` | **Date**: 2026-03-21
**Base path**: `/proxyhosts`
**Authentication**: JWT Bearer (all endpoints require a valid token)
**Error format**: RFC 9457 Problem Details (`application/problem+json`)

---

## GET /proxyhosts

List all proxy hosts (paginated).

**Query parameters**:

| Parameter | Type | Default | Max | Description |
|-----------|------|---------|-----|-------------|
| page | int | 1 | — | 1-based page number |
| pageSize | int | 20 | 100 | Items per page |

**Wolverine message**: `GetProxyHostsQuery { Page, PageSize }`

**Response 200 OK**:
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "domainNames": ["api.west94.io"],
      "destination": "https://backend-api:8080",
      "isEnabled": true,
      "certificate": null
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

**Response 401 Unauthorized**: Missing or invalid JWT bearer token.

---

## GET /proxyhosts/{id}

Get a single proxy host by ID.

**Path parameters**: `id` — Guid

**Wolverine message**: `GetProxyHostByIdQuery { Id }`

**Response 200 OK**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "domainNames": ["api.west94.io", "api-v2.west94.io"],
  "destination": "https://backend-api:8080",
  "isEnabled": true,
  "certificate": {
    "certificatePath": "/certs/api.west94.io.pem",
    "keyPath": "/certs/api.west94.io.key"
  }
}
```

**Response 401 Unauthorized**: Missing or invalid JWT bearer token.
**Response 404 Not Found**: No proxy host with the given ID.

---

## POST /proxyhosts

Create a new proxy host.

**Wolverine message**: `CreateProxyHostCommand { DomainNames, DestinationUri, CertificatePath?,
CertificateKeyPath?, ActorId }`

**Request body** (`application/json`):
```json
{
  "domainNames": ["api.west94.io"],
  "destinationUri": "https://backend-api:8080",
  "certificatePath": "/certs/api.west94.io.pem",
  "certificateKeyPath": "/certs/api.west94.io.key"
}
```

**Required fields**: `domainNames` (non-empty array), `destinationUri` (absolute URI, http/https)
**Optional fields**: `certificatePath`, `certificateKeyPath`

**Response 201 Created** (Location header set to `/proxyhosts/{id}`):
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "domainNames": ["api.west94.io"],
  "destination": "https://backend-api:8080",
  "isEnabled": true,
  "certificate": {
    "certificatePath": "/certs/api.west94.io.pem",
    "keyPath": "/certs/api.west94.io.key"
  }
}
```

**Response 400 Bad Request** (validation errors):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "domainNames": ["At least one domain name is required."],
    "destinationUri": ["'destinationUri' must be a valid absolute http or https URI."]
  }
}
```

**Response 401 Unauthorized**: Missing or invalid JWT bearer token.

**Response 409 Conflict**: A proxy host with one of the provided domain names already exists.
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A proxy host with domain name 'api.west94.io' already exists."
}
```

---

## PUT /proxyhosts/{id}

Update an existing proxy host. All fields are optional; only provided fields are applied.

**Path parameters**: `id` — Guid

**Wolverine message**: `UpdateProxyHostCommand { Id, DomainNames?, DestinationUri?, IsEnabled?,
CertificatePath?, CertificateKeyPath?, ActorId }`

**Request body** (`application/json`):
```json
{
  "domainNames": ["api.west94.io", "api-v2.west94.io"],
  "destinationUri": "https://new-backend:9090",
  "isEnabled": false,
  "certificatePath": null,
  "certificateKeyPath": null
}
```

All fields optional. A field present with `null` explicitly clears the optional value
(e.g., `certificatePath: null` removes the certificate). A field absent entirely is left unchanged.

**Response 200 OK**: Updated proxy host (same shape as GET /proxyhosts/{id}).

**Response 400 Bad Request**: Validation errors (same shape as POST 400).
**Response 401 Unauthorized**: Missing or invalid JWT bearer token.
**Response 404 Not Found**: No proxy host with the given ID.

---

## DELETE /proxyhosts/{id}

Permanently delete a proxy host.

**Path parameters**: `id` — Guid

**Wolverine message**: `DeleteProxyHostCommand { Id, ActorId }`

**Response 204 No Content**: Proxy host deleted.
**Response 401 Unauthorized**: Missing or invalid JWT bearer token.
**Response 404 Not Found**: No proxy host with the given ID.

---

## Common Error Responses

All error responses follow RFC 9457 Problem Details:

| Status | Condition |
|--------|-----------|
| 400 | Missing or invalid request fields |
| 401 | No or invalid JWT bearer token |
| 404 | Resource not found |
| 409 | Hostname conflict on create |
| 500 | Unexpected server error (detail suppressed in non-Development) |
