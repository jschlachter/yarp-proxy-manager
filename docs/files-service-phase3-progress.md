# Implementation Progress

- **Template loaded from:** `implement-plan/assets/progress-tracker-template.md`
- **Plan:** `docs/files-service-plan.md` — Phase 3 (HTTP contract)
- **Status:** `Complete`
- **Updated:** 2026-07-26

`Complete` = all rows `Verified` or user-approved `Descoped` + validation passed + final review `Clear` + nothing material open.

Parent = sole tracker writer under concurrency.

**User directive this phase:** no new integration tests against Postgres (Testcontainers has problems under Podman) — Phase 3 verification is unit tests only, plus the existing env-var-gated manual integration tests already in the repo (unchanged).

## Tasks / subtasks

| ID | Plan ref / requirement | Deps | Status | Acceptance check | Evidence |
|---|---|---|---|---|---|
| T01 | Upload options (`MaxUploadBytes`, `StagingTtl`, sweep interval, per-assetType extension allowlist) | — | Verified | `dotnet build` | `Options/UploadOptions.cs` |
| T02 | `Contracts/FileAssetDto.cs` + `Contracts/PagedResult.cs` | — | Verified | `dotnet build` | `Contracts/FileAssetDto.cs`, `Contracts/PagedResult.cs` |
| T03 | `Endpoints/ClaimsPrincipalExtensions.cs` | — | Verified | `dotnet build` | `Endpoints/ClaimsPrincipalExtensions.cs` |
| T04 | `Validation/UploadContentValidator.cs` + `UnsupportedAssetContentException` | T01 | Verified | 7 unit tests pass | `Unit/UploadContentValidatorTests.cs` |
| T05 | `Assets/FileAssetNotFoundException.cs` | — | Verified | `dotnet build` | `Assets/FileAssetNotFoundException.cs` |
| T06 | `Services/IFileAssetService.cs` + `FileAssetService.cs` (stage/commit/get/content/list/delete) | T01,T04,T05 | Verified | 9 unit tests pass | `Unit/FileAssetServiceTests.cs` |
| T07 | `Services/StagedAssetSweeper.cs` (hosted service) + `StagedAssetSweepRunner.cs` (testable core) | T06 | Verified | 3 unit tests pass | `Unit/StagedAssetSweepRunnerTests.cs` |
| T08 | `Endpoints/FileEndpoints.cs` — 6 routes, streaming multipart (MultipartReader, no `IFormFile`/`ReadFormAsync`), 3-layer size cap, error mapping | T02-T06 | Verified | `dotnet build` | `Endpoints/FileEndpoints.cs` |
| T09 | `Program.cs` wiring — JWT bearer auth, Kestrel body-size config, map endpoints, register sweeper | T08 | Verified | `dotnet build` | `Program.cs` |
| T10 | Unit tests: validator, service (hand-written fakes, no Moq), sweeper eligibility | T04,T06,T07 | Verified | `dotnet test --filter-not-trait "Category=Integration"` → 38/38 pass | test run output |
| T11 | Config: `appsettings.json` `Upload`, `Authentication`, `Kestrel:Limits:MaxRequestBodySize` sections | T09 | Verified | `dotnet build` | `appsettings.json` |

## Loop log (optional, keep brief)

| ID | Owner | Worktree / isolation | Checks | Review | Cleanup |
|---|---|---|---|---|---|
| T01-T11 | parent (direct, no subagent delegation — single cohesive vertical slice) | none | `dotnet build`, `dotnet test --filter Category!=Integration` | self-review pass | n/a |

## Reviews

| Checkpoint | Reviewer | Findings | Disposition | Closure |
|---|---|---|---|---|
| Phase 3 complete | Parent (self-review; no independent reviewer subagent invoked this pass — noted limit) | `IHttpMaxRequestBodySizeFeature` accessed with `!` could NRE under non-Kestrel hosts (e.g. `TestServer`) | Fix now | Clear |

## Decisions / deviations

| Item | Need / change | Evidence | Status |
|---|---|---|---|
| No Testcontainers-based integration tests added for Phase 3 gate | User directive: Testcontainers has problems with Podman | User instruction in this session | Accepted |
| `GET /files` and list service method require both `ownerType`+`ownerId` (400 if missing) | `IFileAssetRepository.GetByOwnerAsync` only supports owner-scoped queries; no unscoped list exists | Repository contract from Phase 2 | Accepted |
| Upload endpoint takes `assetType` as a query parameter | Contract doesn't specify how assetType is supplied; multipart body carries only the file bytes | Plan §Phase 3 HTTP contract table | Accepted |
