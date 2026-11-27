# ADR 0006: Audit log view

**Status:** Accepted

## Context

Users need a page where they can see what changed in the application, who changed it, and when.
For now every authenticated user may view it. Role-based access can come later.

An `audit_log_entries` table already exists (`AuditLogEntry`, `IAuditLogRepository`), but it has
these gaps:

- **Only proxy hosts are recorded.** The create, update and delete handlers for proxy hosts append
  entries. Certificate create, update and delete, and certificate assignment
  (`AssignCertificateHandler`), record nothing. The table is keyed by `proxy_host_id`, so it can't
  hold any other kind of resource.
- **The actor is unreadable.** `ActorId` is the OIDC `sub`, which is an opaque Authentik ID.
- **Deleted resources are hard to identify.** The only description of a deleted resource is inside
  the `previous_state` JSON. A `Created` entry stores no state at all (`newState: null`).
- **Nothing reads the entries.** No endpoint returns them. `GetAllAsync` and `GetByProxyHostAsync`
  support paging but return no total count and have no filters beyond a date range.

## Decision

1. **Store entries for any resource.** Replace `ProxyHostId` with `ResourceType` (`ProxyHost` or
   `Certificate`, stored as a string) and `ResourceId`. Add `ResourceName`, captured when the entry
   is written: the first domain name for a host, or the name for a certificate. Add `ActorName`,
   taken from the token's `preferred_username` claim, falling back to `name`. One migration renames
   the column and backfills `resource_type = 'ProxyHost'`. Existing rows keep a null `ActorName`
   and `ResourceName`.
2. **Audit certificate changes as well.** Certificate create, update and delete each write an
   entry. Certificate assignment writes an `Updated` entry for the proxy host, with its state
   before and after. `Created` entries now store the new state. The stored state is always the
   resource's DTO, so certificate passphrases are never recorded.
3. **One filtered query.** `GET /api/audit-log` (`RequireAuthorization()`, so any authenticated
   user) takes these filters: `from`/`to` (applied to `OccurredAt`), `resourceType`, `operation`,
   `actor` (case-insensitive substring match on actor name or ID), `search` (case-insensitive
   substring match on resource name), and `page`/`pageSize` (default 25, max 100). It returns a
   `PagedResult<AuditLogEntryDto>` sorted newest first. The endpoint dispatches a Wolverine
   `GetAuditLogQuery`, like the other endpoints. One `IAuditLogRepository.QueryAsync` replaces
   `GetAllAsync` and `GetByProxyHostAsync`.
4. **An "Audit Log" UI module.** It has a filter bar, a page-size selector, a results table with
   pagination, and a details dialog that shows the previous and new state side by side. The
   filters are kept in the URL query string, so a filtered view can be shared or bookmarked and the
   browser's Back button works.

## Alternatives

- **Build the view over the existing proxy-host entries only.** This is the simplest option that
  could work: add an endpoint and a page, and change no schema. It was rejected because
  certificate activity, including assignment changes that alter live TLS, would stay invisible.
  The table is still small, so generalizing it now costs little.
- **Resolve actor names at read time** through Authentik's API. This adds an outbound dependency
  and credentials to a read path. Storing the name when the entry is written is simpler, and it
  also shows who the actor was at that moment.
- **Write audit entries from a Wolverine handler that subscribes to the domain events.** The
  events don't carry the actor or the previous state, so they would need to grow. Appending inside
  each command handler matches the existing proxy-host handlers.
- **Full-text search over the state JSON.** This is more than "basic filtering" needs. A substring
  match on the resource name covers finding a host or a certificate.

## Consequences

- Entries written before this change show the raw `sub` as the actor and have no resource name.
  The UI falls back to the resource ID for them.
- Every command now carries `ActorName`, so endpoints share one claims helper.
- Appending an audit entry is still not transactional with the change it records. This is
  unchanged from today.
- The API returns the stored state JSON in list rows. At 100 rows per page this payload is
  acceptable, and the details dialog needs no second request.
- Retention is still unenforced: `AuditOptions.RetentionDays` exists, but nothing calls
  `PurgeOlderThanAsync`. That work is separate from this view.
