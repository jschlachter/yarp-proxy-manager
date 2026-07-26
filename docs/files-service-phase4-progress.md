# Implementation Progress

- **Template loaded from:** `implement-plan/assets/progress-tracker-template.md`
- **Plan:** `docs/files-service-plan.md` — Phase 4 (Certificate integration)
- **Status:** `Complete`
- **Updated:** 2026-07-26

`Complete` = all rows `Verified` or user-approved `Descoped` + validation passed + final review `Clear` + nothing material open.

Parent = sole tracker writer under concurrency.

**Continuation of Phase 3 directive:** no new Postgres/Testcontainers integration tests. Existing Postgres-integration tests (`CertificateEndpointsTests`, `PostgresCertificateRepositoryTests`) must be updated to compile against the new contract (mandatory — otherwise the solution doesn't build) but no new integration test classes/methods are being added beyond that.

## Tasks / subtasks

| ID | Plan ref / requirement | Deps | Status | Acceptance check | Evidence |
|---|---|---|---|---|---|
| T01 | `CertificateSubjectInfo` value object (Core) | — | Verified | build | `AggregatesModel/CertificateAggregate/CertificateSubjectInfo.cs` |
| T02 | `Certificate` aggregate rewrite — asset ids, denormalized filenames, subject fields | T01 | Verified | build | `AggregatesModel/CertificateAggregate/Certificate.cs` |
| T03 | `X509CertificateInspector` (Core, pure static, no host) | T01 | Verified | 9 unit tests pass | `Unit/X509CertificateInspectorTests.cs` |
| T04 | `CertificateDto` rewrite | T02 | Verified | build | `DTOs/CertificateDto.cs` |
| T05 | `CreateCertificateCommand` rewrite (asset ids not paths) | — | Verified | build | `Messages/Commands/CreateCertificateCommand.cs` |
| T06 | EF migration on `ProxyManagerDbContext` — drop path columns, add asset/subject columns | T02 | Verified | `dotnet ef migrations add` scaffolded cleanly; hand-edited to drop+add `certificate_path`→`subject` instead of EF's inferred rename | `Migrations/20260726213349_AddCertificateFileAssets.cs` |
| T07 | `CertificateRecord` + `CertificateConfiguration` + `PostgresCertificateRepository` updated for new schema | T06 | Verified | build | Infrastructure/Data, Repositories |
| T08 | `IFileAssetClient` + `FileAssetClient` (typed HttpClient, API → Files) | — | Verified | build | `Infrastructure/Files/*.cs` |
| T09 | Files: `ServiceTokenAuthenticationHandler` + options — second auth scheme for service-to-service calls | — | Verified | build | `Auth/ServiceTokenAuthenticationHandler.cs` |
| T10 | `CreateCertificateHandler` rewrite — fetch bytes, inspect, create, commit | T02,T03,T05,T08 | Verified | 9 unit tests pass (fakes) | `Unit/Handlers/CreateCertificateHandlerTests.cs` |
| T11 | `DeleteCertificateHandler` shrink — no file I/O | — | Verified | 3 unit tests pass | `Unit/Handlers/DeleteCertificateHandlerTests.cs` |
| T12 | `CertificateEndpoints.cs` — request DTO uses asset ids | T05 | Verified | build | `Endpoints/CertificateEndpoints.cs` |
| T13 | Startup reconciliation hosted service (API) — re-drive commit for still-Staged assets | T08 | Verified | build | `Services/CertificateAssetReconciliationService.cs` |
| T14 | Files: `IFileAssetService.DeleteByOwnerAsync` | — | Verified | 2 unit tests pass | `Unit/FileAssetServiceDeleteByOwnerTests.cs` |
| T15 | Files: `CertificateAssetCleanupHandler` (Wolverine consumer of `CertificateDeletedEvent`) | T14 | Verified | 1 unit test pass (direct call, no broker) | `Unit/CertificateAssetCleanupHandlerTests.cs` |
| T16 | Files: Wolverine + RabbitMQ host wiring, gated by `RabbitMQ:Enabled` | T15 | Verified | build | `Program.cs`, `Infrastructure/WolverineOptionsExtensions.cs` |
| T17 | Update existing tests broken by the `Certificate.Create` signature change (unit + gated integration) | T02,T07,T10,T11 | Verified | `dotnet test --filter-not-trait "Category=Integration"` → 131/131 pass | test run output |
| T18 | Core.Tests: `X509CertificateInspectorTests` — runtime-generated fixture certs (self-signed PEM+PFX, matched/mismatched key, wrong passphrase, expired-warns-not-fails) | T03 | Verified | 9/9 pass | same file as T03 |
| T19 | Final build + full non-integration test run + self-review | all | Verified | `dotnet build` 0 errors; 131/131 tests pass | this session |

## Loop log (optional, keep brief)

| ID | Owner | Worktree / isolation | Checks | Review | Cleanup |
|---|---|---|---|---|---|
| T01-T19 | parent (direct — cross-cutting domain/migration/multi-service change; no subagent delegation this phase, recorded as the review-independence limit too) | none | `dotnet build`, `dotnet test --filter-not-trait "Category=Integration"` | self-review pass at end | n/a |

## Reviews

| Checkpoint | Reviewer | Findings | Disposition | Closure |
|---|---|---|---|---|
| Phase 4 complete | Parent (self-review; no independent reviewer subagent invoked — cross-cutting domain/EF/multi-service change kept in one hand for consistency, recorded as the review-independence limit) | `CreateCertificateHandler` fetched key bytes and ran X509 inspection before checking the PFX+KeyAssetId invariant, wasting a round-trip to Files on a request that was always going to be rejected | Fix now | Clear |

## Decisions / deviations

| Item | Need / change | Evidence | Status |
|---|---|---|---|
| `Certificate.Create` signature includes `certificateFileName`/`keyFileName` params, not shown in the plan's abbreviated snippet | Denormalized filenames are explicitly required on the aggregate ("New denormalized CertificateFileName / KeyFileName") but have no other entry point | Plan §Phase 4 Domain section | Accepted |
| `X509CertificateInspectorTests` use certs generated at test time (`CertificateRequest`) rather than committed binary fixture files | Avoids committing binary blobs to git; equivalent coverage, easier to vary scenarios (expired, mismatched key) | N/A — implementation choice | Accepted |
| Service-to-service auth: custom `ServiceTokenAuthenticationHandler` reads a static shared secret from config/Podman-secret-mapped env var, added as a second scheme alongside JWT bearer via `AuthorizationPolicyBuilder` | Plan explicitly calls this "a deliberate temporary shortcut" pending OAuth2 client-credentials (Deferred #3) | Plan §Phase 3 Auth section (token seam) + §Phase 4 | Accepted |
| Files' RabbitMQ wiring uses `DeclareExchange("certificates", ex => { ex.BindQueue(...); })` + `ListenToRabbitQueue(...)`, not the plan's literal `ListenToRabbitQueue(...).BindExchange(...)` | `RabbitMqListenerConfiguration` in WolverineFx.RabbitMQ 6.22.0 has no `BindExchange` method — confirmed via reflection over the installed package; binding a queue to an exchange is done from the exchange side (`IRabbitMqBindableExchange.BindQueue`) | Reflected `Wolverine.RabbitMQ.dll` 6.22.0 method list this session | Accepted |
| Existing Postgres-integration tests (`CertificateEndpointsTests`, `PostgresCertificateRepositoryTests`) updated in place to compile against the new asset-based contract; `TestWebAppFactory` now substitutes a `FakeFileAssetClient` for the real Files HTTP client | Mandatory for the solution to build/test at all — `CreateCertificateHandler` now depends on `IFileAssetClient`, which has no live Files service in the Postgres-testcontainer host | This session, continuing the no-new-Testcontainers-integration-tests directive from Phase 3 | Accepted |
