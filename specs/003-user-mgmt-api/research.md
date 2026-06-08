# Research: User Management API (003-user-mgmt-api)

> **Revision note (2026-04-18)**: Added decision on `ProfileImageUrl` validation and `Nickname` field distinction.

## ProfileImageUrl Validation

**Decision**: `ProfileImageUrl` is stored as a plain `string?`. Validation on write: if the value is non-null and non-empty, it MUST be parseable as an absolute `http` or `https` URI (using `Uri.TryCreate` + `Uri.IsAbsoluteUri` check in the aggregate). No HTTP fetch or reachability check is performed at write time.

**Rationale**: The API stores a reference to an externally hosted image (e.g., Authentik avatar, Gravatar). Storing raw bytes would inflate the response payload and add hosting complexity. Validating URL format at write time prevents clearly invalid values while keeping the implementation lightweight. Reachability validation is inappropriate for a management API — the image host may be on a private network.

**Alternatives considered**:
- Store image data as Base64: Rejected — inflates payload, not needed since all clients have network access to Authentik's image URLs.
- No validation: Rejected — garbage URLs degrade consumer UX.

---

## Nickname vs. DisplayName

**Decision**: Two separate fields. `DisplayName` = full formal name (e.g., "Alice Anderson"). `Nickname` = preferred short display name (e.g., "Alice"). Both are required strings.

**Rationale**: UI components typically show a shortened name in navigation bars, avatars, and notifications, while formal name is needed for reports and audit logs. Combining them into one field forces consumers to implement their own truncation logic inconsistently.

**Alternatives considered**:
- Single `Name` field: Rejected — consumers would have to guess at preferred short form.
- Optional `Nickname`: Rejected — if the field exists it should always be populated for consistent UI rendering; the caller (who knows the user) can always provide a nickname equal to the first name.

---

## Role-Based Authorization with JWT Bearer

**Decision**: Use a named ASP.NET Core authorization policy (`"UserAdmin"`) backed by a custom claim `pm_role` (Proxy Manager role). Authentik issues JWTs containing this claim; the API reads it without further mapping.

**Rationale**: The existing codebase uses `AddAuthorization()` without named policies — any authenticated user passes. Adding a named policy is the minimal extension needed for two-tier access control. Using a project-specific claim (`pm_role`) avoids collision with Authentik's built-in `roles`/`groups` claims (which control Authentik's own UI, not application roles). `RequireAuthorization("UserAdmin")` on the endpoint group cleanly separates write from read endpoints.

**Alternatives considered**:
- ASP.NET Core role middleware (`[Authorize(Roles = "Admin")]`): Requires mapping Authentik's role claim to `ClaimTypes.Role` via `JwtBearerOptions.MapInboundClaims`, which is a hidden global side-effect. Rejected for being harder to reason about.
- Separate audience per role: Overkill for two roles in the same service. Rejected.

**Claim contract**: Authentik must be configured to include `pm_role` in the token with value `"Admin"` or `"ReadOnly"`. Operators without this claim are rejected at the endpoint level.

---

## API Versioning Strategy

**Decision**: Route-prefix versioning via `MapGroup("/v1")` nested inside the existing application — no versioning library.

**Rationale**: The project already uses minimal APIs with `MapGroup`. A simple `/v1` prefix is zero-dependency, readable in route tables, and consistent with the spec's stated example paths (`/v1/users`). The existing `/proxyhosts` endpoints are intentionally left unversioned (out of scope for this feature).

**Alternatives considered**:
- `Asp.Versioning` NuGet package: Adds query-string, header, and URL versioning strategies plus deprecation metadata. Justified only if multiple versions of the same endpoint need to coexist. Rejected (YAGNI).
- Header versioning (`API-Version: 1`): Invisible in browser tooling and Scalar UI. Rejected for developer ergonomics.

---

## Soft Delete / Reactivation Pattern

**Decision**: `AuthorizedUser` carries a `Status` enum (`Active`/`Deactivated`) and a nullable `DeactivatedAt` timestamp. Deactivation sets both; reactivation clears `DeactivatedAt`, sets `Status = Active`, and updates `AccessLevel`. A separate `UserOperation` enum value `Reactivated` distinguishes it in the audit log from `Created`.

**Rationale**: Keeping the record in place preserves full audit history and makes reactivation a simple field update rather than a re-insert. The `Status` flag is cheaper to filter on than checking `DeactivatedAt is null`.

**Alternatives considered**:
- Separate `ArchivedUser` table/collection: Adds complexity without benefit for in-memory storage. Rejected.
- Tombstone-only pattern (no status field, just `DeletedAt`): Requires null-check in every query. Rejected for clarity.

---

## Audit Atomicity (In-Memory)

**Decision**: Handlers write to the user repository first, then to the audit repository. Both are injected as singletons. If the audit write throws, the exception propagates to Wolverine, which surfaces it to the endpoint as an unhandled error (500). This satisfies FR-009's "silent failures are not acceptable" requirement. Compensating rollback of the user write is not implemented because in-memory ConcurrentDictionary operations are not transactional — the cost of rollback complexity is not justified for an in-memory store. The spec assumption is that a persistent store will be added later; at that point, a proper transaction/unit-of-work can be introduced.

**Rationale**: Simple, consistent with the existing handler pattern for ProxyHost operations.

**Alternatives considered**:
- Manual rollback after audit failure: Fragile and error-prone for in-memory stores. Rejected.
- Outbox pattern: Appropriate for distributed systems with durable messaging. Out of scope here.

---

## User Identifier Routing

**Decision**: The `sub` claim value (an Authentik-issued UUID string, e.g., `abc123de-...`) is used as the route segment: `GET /v1/users/{sub}`. Since Authentik `sub` values are UUIDs, no URL encoding is needed. The route parameter type is `string` (not `Guid`) to honour the spec's requirement to treat it as an opaque value.

**Rationale**: Using `sub` directly as the route key removes any need for an internal ID ↔ sub mapping table. The client always knows the `sub` from their own JWT.

**Alternatives considered**:
- Internal Guid as route key: Requires a lookup from `sub` → internal ID in every request. Rejected (unnecessary indirection).

---

## Reactivation HTTP Response Code

**Decision**: POST `/v1/users` returns **200 OK** (with the updated `AuthorizedUserDto`) when reactivating a deactivated user, instead of 201 Created. A response header `X-User-Reactivated: true` signals the reactivation to callers that need to distinguish creation from reactivation.

**Rationale**: RFC 9110 defines 201 as "a new resource was created". Reactivation restores an existing record — the URI (`/v1/users/{sub}`) already existed before the request. Returning 201 with the same location would be semantically incorrect. 200 accurately reflects "the request succeeded and here is the current state of the resource." The `X-User-Reactivated` header allows clients to handle the two outcomes differently without parsing the body.

**Alternatives considered**:
- Always 201 regardless: Semantically incorrect for reactivation. Rejected.
- Separate `POST /v1/users/{sub}/reactivate` endpoint: More RESTful but adds an extra endpoint and client complexity for an infrequent operation. Rejected (spec says to handle via POST to the create endpoint).
