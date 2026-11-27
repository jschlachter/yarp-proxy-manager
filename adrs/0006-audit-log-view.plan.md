# Plan: Audit log view (ADR 0006)

**Status:** Ready

## Goal

Any signed-in user can open **Audit Log** in the UI and see management activity for proxy hosts
and certificates, newest first. Each entry shows who made the change, what changed and when. The
list can be filtered by time range, resource type, operation, actor and resource name. The page
size is 25, 50 or 100, and the user moves through pages. Each entry opens a dialog that shows the
state before and after the change.

**Out of scope:**
- Role-based access to the audit log. Every authenticated user can see it for now.
- Retention and purging (`AuditOptions.RetentionDays` is still unused).
- Auditing the files service, logins, or proxy traffic.
- Exporting entries, and "history" links from the host and certificate detail pages.
- Backfilling actor or resource names on existing rows.

## Tasks

### 1. Generalize the audit entry — [ ] todo

- `src/ProxyManager.Core/AggregatesModel/AuditLogAggregate/AuditLogEntry.cs`:
  - Add `public enum AuditResourceType { ProxyHost, Certificate }`.
  - Replace `ProxyHostId` with `ResourceType` and `ResourceId`.
  - Add `string? ResourceName` and `string? ActorName`.
  - Update the `Create(...)` signature to take the new fields.
- `IAuditLogRepository`: replace `GetByProxyHostAsync` and `GetAllAsync` with
  `Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> QueryAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken ct)`.
  Add the `AuditLogFilter` record (`From`, `To`, `ResourceType?`, `Operation?`, `Actor?`,
  `Search?`) in the same folder.
- `AuditLogEntryConfiguration`:
  - Map `resource_type` as a string (`HasConversion<string>()`, max length 32).
  - Rename `proxy_host_id` to `resource_id`.
  - Map `resource_name` (max length 256) and `actor_name` (max length 256).
- Add a migration:
  `dotnet ef migrations add GeneralizeAuditLog --project src/ProxyManager.Infrastructure`.
  Hand-edit it to use `RenameColumn` instead of drop-and-add, and add `resource_type` with
  `defaultValue: "ProxyHost"` so existing rows are backfilled.
- `PostgresAuditLogRepository.QueryAsync`:
  - Apply each filter only when it is set.
  - Match `Actor` with `EF.Functions.ILike` on `ActorName` or `ActorId`.
  - Match `Search` with `ILike` on `ResourceName`.
  - Escape `%` and `_` in the user's input.
  - Run `CountAsync` before `Skip`/`Take`, and sort by `OccurredAt` descending.
- Make the same change to `InMemoryAuditLogRepository` and the test fake
  `tests/ProxyManager.API.Tests/Unit/Fakes/FakeAuditLogRepository.cs`. Both match with
  case-insensitive `Contains`.

**Verify:**
- Update `tests/ProxyManager.Core.Tests/Unit/AuditLogEntryTests.cs`.
- Update `InMemoryAuditLogRepositoryTests` to cover each filter, the total count when results span
  more than one page, and newest-first order.
- Extend the integration test `PostgresAuditLogRepositoryTests` to cover the `ILike` actor and
  search filters, a search string containing `%`, and a date range.
- Apply the migration to a database with existing rows. Check that they read back as `ProxyHost`
  with the same `resource_id`.

### 2. Carry the actor's name on commands — [ ] todo

- Add `src/ProxyManager.API/Endpoints/ClaimsPrincipalExtensions.cs`, following the files
  service's version: `GetActorId()` reads `NameIdentifier`, then `sub`, then `"unknown"`, and
  `GetActorName()` reads `preferred_username`, then `name`, then `Identity.Name`.
- Replace the inline `actorId` lookups in `ProxyHostEndpoints` and `CertificateEndpoints` with
  these helpers.
- Add `string? ActorName` to the commands that carry `ActorId`:
  - `CreateProxyHostCommand`, `UpdateProxyHostCommand`, `DeleteProxyHostCommand`
  - `AssignCertificateCommand`
  - `CreateCertificateCommand`, `UpdateCertificateCommand`, `DeleteCertificateCommand`

**Verify:** the solution builds. Then check that a real Authentik access token carries
`preferred_username`. Decode one in the dev setup, or log the claim once. If the token doesn't
carry it, add the `profile` scope to the API's Authentik provider. Don't switch to a different
claim.

### 3. Write audit entries for every mutation — [ ] todo

- Proxy host handlers (create, update, delete):
  - Pass `AuditResourceType.ProxyHost`, `ResourceName` (the first domain name) and `ActorName`.
  - `CreateProxyHostHandler`: record the serialized DTO as `newState`. It is null today.
- `AssignCertificateHandler`:
  - Inject `IAuditLogRepository`.
  - Serialize the host DTO before and after the assignment, and append an `Updated` entry for the
    `ProxyHost`.
- Certificate handlers (create, update, delete):
  - Inject `IAuditLogRepository`.
  - Append `Created`, `Updated` and `Deleted` entries for the `Certificate`, with the DTO from
    `GetCertificatesHandler.MapToDto` as the before and after state, and `ResourceName = cert.Name`.
  - In `CreateCertificateHandler`, append the entry after the asset commits succeed, so a failed
    create doesn't leave an entry behind.

**Verify:**
- Extend the existing handler tests in `tests/ProxyManager.API.Tests/Unit/Handlers/`, using
  `FakeAuditLogRepository`:
  - Each certificate handler appends one entry with the right operation, resource type, ID and
    name.
  - `AssignCertificateHandler` appends one `Updated` entry whose new state contains the
    certificate ID.
  - No certificate entry's state contains the passphrase.
  - `CreateProxyHostHandler` now stores the new state.

### 4. `GET /api/audit-log` — [ ] todo

- Core:
  - Add `Messages/Queries/GetAuditLogQuery.cs`, which carries the filter fields and
    `Page`/`PageSize`.
  - Add `DTOs/AuditLogEntryDto.cs` with `Id`, `OccurredAt`, `ActorId`, `ActorName`, `Operation`,
    `ResourceType`, `ResourceId`, `ResourceName`, `PreviousState` and `NewState`. The enums are
    strings and the state fields are raw JSON strings.
- API:
  - Add `Handlers/GetAuditLogHandler.cs`, which maps the query to an `AuditLogFilter`, calls
    `QueryAsync`, and returns a `PagedResult<AuditLogEntryDto>`.
  - Add `Endpoints/AuditLogEndpoints.cs`, with `MapGroup("/api/audit-log").RequireAuthorization()`
    and `MapGet("/")`.
  - The endpoint validates its query string:
    - `page` must be at least 1.
    - `pageSize` must be between 1 and 100. The default is 25.
    - `resourceType` and `operation` must parse, ignoring case.
    - `from` must not be later than `to`.
  - Invalid input returns `TypedResults.Problem` with status 400, matching the existing
    endpoints.
  - Call `app.MapAuditLogEndpoints()` in `Program.cs`.

**Verify:**
- Unit-test `GetAuditLogHandler`: the filters are passed through, the DTO is mapped, and the
  `PagedResult` totals are correct.
- Add `tests/ProxyManager.API.Tests/Integration/AuditLogEndpointsTests.cs`, following
  `ProxyHostEndpointsTests`:
  - An unauthenticated request gets 401.
  - Creating a host and then a certificate returns both entries, newest first.
  - `resourceType=Certificate` returns only the certificate entry.
  - `pageSize=1` returns `TotalCount = 2`.
  - `pageSize=500` and `from > to` each return 400.

### 5. UI: client, BFF route, module — [ ] todo

- `src/ProxyManager.UI/types`: add the `AuditLogEntry` type.
- `lib/proxy-manager-client.ts`: add `listAuditLog(session, params)`, which builds the query
  string from the filters that are set.
- `app/api/audit-log/route.ts`:
  - Add a `GET` handler that passes through only the known query parameters and clamps
    `pageSize` to 100, following `app/api/certificates/route.ts`.
  - Don't add an admin check.
- `lib/modules.ts`: add `{ label: "Audit Log", href: "/audit-log", icon: HistoryIcon, enabled: true }`.

**Verify:**
- Add a Jest test under `tests/unit/api/` for the BFF route: parameters are passed through,
  unknown parameters are dropped, `pageSize` is clamped, and upstream errors map to a problem
  response.
- Update `tests/e2e/modules.spec.ts` to expect the new navigation entry.

### 6. UI: audit log page — [ ] todo

- `app/(dashboard)/audit-log/page.tsx`: the page header (title "Audit Log", with the
  `text-gradient` style used on other pages), plus `AuditLogClient`.
- `app/(dashboard)/audit-log/AuditLogClient.tsx`:
  - **State:** the filters and page are read from and written to the URL search parameters
    (`useSearchParams` plus `router.replace`). Data comes from `/api/audit-log` through
    `lib/api-fetch.ts`.
  - **Filter bar:**
    - A time range preset (last 24 h, 7 days or 30 days, all time, or custom from/to dates).
    - A resource type select and an operation select.
    - Actor and search text inputs, debounced by about 300 ms.
    - A "Clear filters" button.
    - Changing any filter resets to page 1.
  - **Table** (`components/ui/table.tsx`), with these columns:
    - When: relative time, with the exact local time on hover.
    - Actor: `actorName ?? actorId`.
    - Action: an operation badge.
    - Resource: a type icon plus `resourceName ?? resourceId`. The name links to the resource's
      detail page unless the operation is `Deleted`.
    - A "Details" button.
  - **States:** a skeleton while loading, an empty state when no entry matches, and an error
    state for a problem response.
  - **Footer:** "Showing X–Y of N", Previous and Next buttons, and a 25/50/100 page-size select.
- `components/audit-log/AuditLogDetailDialog.tsx`: shows the previous and new state as formatted
  JSON side by side (stacked on narrow screens). Top-level keys that differ are highlighted, and
  a missing side shows "—".
- Style it to match the existing dashboard: dark, vibrant, not stock shadcn grey.

**Verify:**
- Jest component tests for `AuditLogClient`:
  - Changing a filter updates the URL and resets the page.
  - The pagination buttons are disabled at the first and last page.
  - The empty state renders when there are no results.
- Jest component tests for the detail dialog: changed keys are highlighted, and the dialog
  handles a null previous state or new state.
- Add a Playwright spec `tests/e2e/audit-log.spec.ts`: create a route, open Audit Log, see the
  entry, filter by resource type, and open its details.
- Manual check in the browser with `npm run dev` and "Launch It All": edit a host, assign a
  certificate, and delete a certificate. Each change appears with the actor's name, and the
  filters and page size behave as expected.

## Open questions

None. The actor name is the Authentik username (`preferred_username`), as decided on 2026-10-04.
