# Contract: Audit Log API

## New Endpoint

### GET /proxyhosts/{id}/audit

Returns paginated audit log entries for a specific ProxyHost, with optional time-range filtering.

**Authorization**: Bearer JWT (same requirement as all `/proxyhosts` endpoints)

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `uuid` | ProxyHost identifier |

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `from` | `ISO 8601 datetime` | none | Inclusive lower bound on `occurred_at` |
| `to` | `ISO 8601 datetime` | none | Inclusive upper bound on `occurred_at` |
| `page` | `int` | `1` | 1-based page number |
| `pageSize` | `int` | `50` | Items per page; max 200 |

**Success Response** `200 OK`

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "proxyHostId": "7b1e3c9d-83a4-4f1b-b2d8-9e0c1f2a3b4c",
      "actorId": "user@example.com",
      "operation": "Updated",
      "previousState": "{\"domainNames\":[\"old.example.com\"],\"isEnabled\":true,...}",
      "newState": "{\"domainNames\":[\"new.example.com\"],\"isEnabled\":true,...}",
      "occurredAt": "2026-06-07T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 12
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | `pageSize` > 200, or invalid datetime format — RFC 9457 Problem Details |
| `401 Unauthorized` | Missing or invalid Bearer token |
| `404 Not Found` | No ProxyHost with given `id` exists |

**Notes**
- Entries are ordered by `occurredAt` descending (most recent first).
- Entries for deleted ProxyHosts are preserved in the audit log but the endpoint returns 404 since the ProxyHost no longer exists. To query historical entries for deleted hosts, use the global audit endpoint (future feature).
- The `operation` field is a string enum: `"Created"`, `"Updated"`, `"Deleted"`.

---

## Existing Endpoint Changes

No breaking changes to existing `/proxyhosts` endpoints. All existing request/response shapes are unchanged.

The `ProxyHostDto` returned by `/proxyhosts` endpoints gains two new read-only fields when the DB-backed implementation is in use:

```json
{
  "id": "...",
  "domainNames": ["example.com"],
  "destinationUri": "https://backend:443",
  "isEnabled": true,
  "createdAt": "2026-06-07T10:00:00Z",
  "updatedAt": "2026-06-07T14:30:00Z"
}
```

`createdAt` and `updatedAt` are ISO 8601 UTC timestamps. Both are nullable in the DTO to remain backwards-compatible with the in-memory implementation used in tests.

---

## RabbitMQ Message Contract

Exchange: `proxy-hosts` (fanout, durable) — already declared.  
No schema changes to existing event messages. The three event types continue to carry the same fields:

| Event | Key fields |
|-------|-----------|
| `ProxyHostCreatedEvent` | `Id`, `DomainNames`, `DestinationUri`, `IsEnabled`, `OccurredAt` |
| `ProxyHostUpdatedEvent` | `Id`, `DomainNames`, `DestinationUri`, `IsEnabled`, `OccurredAt` |
| `ProxyHostDeletedEvent` | `Id`, `OccurredAt` |

ProxyManager subscribes to all three via a Wolverine consumer to trigger live-reload.
