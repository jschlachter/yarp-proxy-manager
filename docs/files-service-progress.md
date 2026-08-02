# files-service-plan — Implementation Progress

- **Template loaded from:** `implement-plan/assets/progress-tracker-template.md`
- **Plan:** `docs/files-service-plan.md`
- **Status:** In progress (Phase 0-5 complete; Phase 6 in progress — see `docs/files-service-phase6-progress.md`)
- **Updated:** 2026-07-26

`Complete` = all rows `Verified` or user-approved `Descoped` + validation passed + final review `Clear` + nothing material open.

This file is the running checklist across sessions for the full 7-phase plan in `docs/files-service-plan.md`. Each session should read it before resuming, and update it before ending.

## Tasks / subtasks

| ID | Plan ref / requirement | Deps | Status | Acceptance check | Evidence |
|---|---|---|---|---|---|
| **Phase 0 — RustFS/AWSSDK spike** | plan lines 39–59 | — | Verified | Put/Get/Head/Delete/Presign round trip against real RustFS | `tests/ProxyManager.Files.Tests/Integration/S3ObjectStoreTests.cs` passed against live RustFS at `localhost:9000` |
| **Phase 1 — Storage abstraction** | plan lines 39–92 | Phase 0 | Verified | `dotnet build ProxyManager.sln` clean; integration test green | `src/ProxyManager.Files` (Options/Storage/Infrastructure/Services), registered in `ProxyManager.sln` |
| T01 | `ProxyManager.Files.csproj` scaffold + sln registration | — | Verified | builds | src/ProxyManager.Files/ProxyManager.Files.csproj |
| T02 | `ObjectStorageOptions` | T01 | Verified | compiles, bound to config | src/ProxyManager.Files/Options/ObjectStorageOptions.cs |
| T03 | `IObjectStore` + `S3ObjectStore` + checksum workaround | T02 | Verified | compiles; integration test | src/ProxyManager.Files/Storage/*.cs |
| T04 | `AddFilesServices` DI registration | T03 | Verified | compiles, DI resolves | src/ProxyManager.Files/Infrastructure/ServiceCollectionExtensions.cs |
| T05 | `BucketBootstrapHostedService` | T04 | Verified | compiles | src/ProxyManager.Files/Services/BucketBootstrapHostedService.cs |
| T06 | `Program.cs` + dev appsettings | T05 | Verified | app starts | src/ProxyManager.Files/Program.cs, appsettings*.json |
| T07 | `tests/ProxyManager.Files.Tests` scaffold + sln registration | T01 | Verified | builds | tests/ProxyManager.Files.Tests |
| T08 | Phase 0 gate as automated integration test | T06,T07 | Verified | passes against live RustFS | Integration/S3ObjectStoreTests.cs |
| **Phase 2 — Asset model and persistence** | plan lines 95–149 | Phase 1 | Verified | Migration applies to a clean DB in `files` schema | Applied to `pod.lab.linux01.west94.io`; `files.__ef_migrations_history` isolated from `ProxyManagerDbContext`'s history (confirmed both list independently) |
| T09 | `FilesDbContext` with `HasDefaultSchema("files")` + custom migrations history table, registered separately from `ProxyManagerDbContext` | Phase 1 | Verified | builds; migration scoped to `files` schema | src/ProxyManager.Files/Data/FilesDbContext.cs, FilesDbContextFactory.cs |
| T10 | `FileAsset` domain model (plain sealed class, not `Core.SeedWork.Entity`) | T09 | Verified | unit tests pass | src/ProxyManager.Files/Assets/FileAsset.cs; tests/.../Unit/FileAssetTests.cs (5 tests) |
| T11 | `FileAssetRecord` + `FileAssetConfiguration` (snake_case columns), indexes `(owner_type, owner_id)` and `(status, created_at)` | T10 | Verified | migration generated both indexes | src/ProxyManager.Files/Data/FileAssetRecord.cs, Configurations/FileAssetConfiguration.cs |
| T12 | `PostgresFileAssetRepository` (private `ToDomain`/`ToRecord`, mirrors `PostgresCertificateRepository`) | T11 | Verified | compiles | src/ProxyManager.Files/Repositories/PostgresFileAssetRepository.cs |
| T13 | Key-scheme helpers (`staging/{uploadId}/{filename}`, `{assetType}/{assetId}/{filename}`), filename sanitization, assetType allowlist | T10 | Verified | 14 unit tests pass | src/ProxyManager.Files/Assets/AssetKeyBuilder.cs; tests/.../Unit/AssetKeyBuilderTests.cs |
| T14 | EF migration generated in `files` schema; DB bootstrap hosted service (mirrors `DatabaseMigrationService`) | T09,T11 | Verified | applied live to shared dev Postgres | Data/Migrations/20260726202028_InitialCreate.cs; Services/FilesDatabaseMigrationService.cs |
| T15 | Unit tests for sanitization/key scheme; migration apply test | T12,T13,T14 | Verified | 19 unit + 2 integration tests pass | tests/ProxyManager.Files.Tests (Unit/, Integration/FilesDbContextMigrationTests.cs) |
| **Phase 3 — HTTP contract** | plan lines 152–208 | Phase 2 | Verified | Integration test uploads/commits/downloads against RustFS | see `docs/files-service-phase3-progress.md` |
| **Phase 4 — Certificate integration** | plan lines 212–281 | Phase 3 | Verified | Cert create/delete round-trips end to end | see `docs/files-service-phase4-progress.md` |
| **Phase 5 — Deployment** | plan lines 285–294 | Phase 4 | Verified | `deploy-vm.sh` brings the full stack up | see `docs/files-service-phase5-progress.md` |
| **Phase 6 — UI** | plan lines 298–306 | Phase 5 | Partial | Real upload works in the browser | see `docs/files-service-phase6-progress.md` — implementation/review done; e2e execution and full `npm run build` blocked on a user decision |
| **Phase 7 — Tests** (lands with the phase it covers, not standalone) | plan lines 310–315 | rolling | Pending | | |

## Loop log

| ID | Owner | Worktree / isolation | Checks | Review | Cleanup |
|---|---|---|---|---|---|
| Phase 0-1 | main agent (direct) | none | build/test | self (low-effort session) | n/a |
| Phase 2 | main agent (direct) | none | build/test/migration-apply | self (low-effort session) | 56 leaked Postgres testcontainers from prior session's `dotnet test` run found and removed (user-approved) |

## Reviews

| Checkpoint | Reviewer | Findings | Disposition | Closure |
|---|---|---|---|---|
| Phase 0-1 complete | none (not requested) | — | — | — |

## Decisions / deviations

| Item | Need / change | Evidence | Status |
|---|---|---|---|
| Testcontainers → live localhost RustFS for Phase 0 gate | User explicit direction, credentials provided for that session | prior conversation | Applied |
| Scope: Phase 0-1 only in first session | User chose smallest slice via AskUserQuestion | prior conversation | Applied |
| Cleaned up 56 leaked Postgres testcontainers (Testcontainers.PostgreSql, `dotnet test ProxyManager.sln` in prior session) | Ryuk reaper appears unable to clean up in this Podman environment (pre-existing, out of plan scope) — leaked containers found while checking for a local Postgres to test against | user approved via AskUserQuestion | Applied |
| Migration applied directly to shared dev Postgres (`pod.lab.linux01.west94.io`), using existing `POSTGRES_PASSWORD` user secret from `ProxyManager.API` | Needed to prove the `files` schema + separate migrations-history-table isolation actually works, not just compiles; only additive (new schema), no existing tables touched | user approved via AskUserQuestion; confirmed via `dotnet ef migrations list` on both contexts independently | Applied |
| Phase 2 migration-apply integration test reads live DB via `PROXYMANAGER_FILES_DB_CONNECTION` env var rather than Testcontainers | Avoid repeating the Ryuk-cleanup leak; the plan's Phase 7 testcontainers-based integration suite is explicitly deferred to a later pass anyway | this session | Applied |
