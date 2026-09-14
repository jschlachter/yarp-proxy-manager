# Dynamic Let's Encrypt certificates via LettuceEncrypt-Archon

> **Status:** Ready for implementation

## Outcome and boundaries

- **Problem and target:** `ProxyManager` (the Kestrel/YARP edge process) currently has no automated TLS certificate issuance. TLS is either a single static SNI entry hardcoded in `appsettings.Development.json`, or a manually-uploaded certificate in the `Certificate` aggregate that is **never actually consumed for TLS termination anywhere** (confirmed by full-repo grep — no `ServerCertificateSelector`/SNI callback exists). `ProxyHost` domains are dynamic (DB-driven, added/removed by admins at runtime), so certs must be issued/renewed per-domain automatically without redeploying config.
- **In scope:**
  - Add `LettuceEncrypt-Archon` (v3.0.0, targets `net10.0`) to `ProxyManager` for automated ACME (HTTP-01) issuance/renewal against Let's Encrypt.
  - A `TlsMode` field on `ProxyHost` (`Manual` | `LetsEncrypt`) so admins opt a host into automatic issuance; `LetsEncrypt`-mode hosts' domains feed a custom `IDomainSource`.
  - Custom `ICertificateSource`/`ICertificateRepository` implementations that persist LE-issued certs into the existing `Certificate` aggregate + `ProxyManager.Files` blob storage, so issued certs appear in the existing certificate UI/API alongside manually-uploaded ones.
  - Wiring Kestrel (`ProxyManager/Program.cs`) so **any** valid certificate in the `Certificate` aggregate — manual or LE-issued — is selectable by SNI hostname match against its stored Subject Alternative Names. This closes the existing "CertificateId is metadata only" gap as a natural side effect of the LE integration.
  - Exposing port 80 (systemd Quadlet + Containerfile) for the ACME HTTP-01 challenge, and adding the missing production `Kestrel` config section.
  - UI: a TLS-mode toggle on the route (ProxyHost) form, and a "Source" badge (Manual / Let's Encrypt) on certificate cards.
- **Out of scope (non-goals):**
  - DNS-01 or TLS-ALPN-01 challenge types, and wildcard certificates — HTTP-01 single-domain/SAN-set only.
  - Renumbering the external HTTPS port (8443 stays as-is); only port 80 is newly exposed for the ACME challenge.
  - Multi-instance/horizontal-scale challenge-response sharing (`IHttpChallengeResponseStore`, `IRuntimeCertificateStore` distributed implementations) — single-instance Quadlet deployment is assumed.
  - Automatic migration/backfill logic beyond defaulting existing certificates' new `Source` column to `Manual`.
  - Admin UI for manual force-renew/revoke; only issuance, automatic renewal, and read-only display are in scope.
  - Rate-limit-aware retry/backoff beyond the library's built-in `FailedRenewalBackoffPeriod`/`OnDemandLookupCooldown` defaults.
  - **Automatic Let's Encrypt issuance for `ProxyManager`'s own edge hostname(s)** (e.g. `proxy-manager.west94.io`, a system route from `proxysettings.json`, not a DB-backed `ProxyHost`). `IDomainSource` in this plan only enumerates `LetsEncrypt`-mode `ProxyHost` rows, so the app's own hostname is never fed to LettuceEncrypt for auto-issuance. It remains on the **Manual** certificate path: `AggregateCertificateSource` will still serve any `Certificate` row for it by SAN match regardless of `ProxyHost` linkage, so the existing `certs/proxy-manager.west94.io.pfx` (previously wired via the now-removed static `Kestrel:Https:Sni` config) must be uploaded as a Manual certificate through the existing certificate API/UI to restore TLS for this hostname before/at deploy. A separate, `ProxyHost`-independent opt-in for LE issuance of the app's own hostname(s) — e.g. a config-driven domain list merged into `IDomainSource` — is a deliberate future follow-up, out of scope here. `scripts/upload-proxy-manager-certificate.sh` automates the manual re-upload (stages the `.pfx` in `ProxyManager.Files`, then creates the `Certificate` resource via `POST /api/certificates`).
- **Approach:** Add `TlsMode` to `ProxyHost` and a `Source` marker to `Certificate` (Core + migration). Implement `IDomainSource` (reads `LetsEncrypt`-mode hosts), `ICertificateSource` (reads all `Certificate` rows, fetches bytes from `ProxyManager.Files`, builds `X509Certificate2`, SAN-indexed), and `ICertificateRepository` (LE's — persists newly issued/renewed certs back into the `Certificate` aggregate + Files, and auto-`AssignCertificate`s the matching `ProxyHost`). Register everything in `ProxyManager/Program.cs` via `AddLettuceEncrypt()` + `UseLettuceEncrypt()` on the Kestrel HTTPS listener. Promote the existing `IFileAssetClient` (currently API-only) into `ProxyManager.Infrastructure` so `ProxyManager` can call `ProxyManager.Files` directly, matching the existing service-to-service pattern.

## Key files, evidence, and decisions

| File or source | Why it matters | Decision or plan impact |
|---|---|---|
| `src/ProxyManager/appsettings.Development.json` (`Kestrel:Endpoints:Https:Sni`) | Only existing TLS config; static, single-domain, no production equivalent exists | Replace with `UseLettuceEncrypt()` on the HTTPS listener; add missing `Kestrel` section to production `appsettings.json` |
| `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/ProxyHost.cs` | `DomainNames`/`CertificateId` exist but no TLS-mode concept; `AssignCertificate` has no validation | Add `TlsMode` property + `SetTlsMode` mutator; `CertificateId` becomes informational/UX-only (selection is by SAN match, not this field) |
| `src/ProxyManager.Core/AggregatesModel/CertificateAggregate/Certificate.cs` | Immutable after creation (`Rename`/`UpdatePassPhrase` only); no origin marker | Add `Source` (`Manual`/`LetsEncrypt`) field set at creation, and a `ReplaceAssets(...)` mutator for LE renewal so `Certificate.Id`/`ProxyHost.CertificateId` references survive renewal |
| `src/ProxyManager.Core/AggregatesModel/CertificateAggregate/ICertificateRepository.cs` | No domain/SAN-filtered query method; only `GetAllAsync` | LE's `ICertificateSource`/`ICertificateRepository` will filter `GetAllAsync()` results in memory (cert volume is small; avoids a new indexed query for this scope) |
| `src/ProxyManager.Infrastructure/Repositories/PostgresCertificateRepository.cs` | `UpdateAsync` persists only `Name`/`PassPhrase`/`UpdatedAt` today | Must extend to also persist `CertificateAssetId`/`KeyAssetId`/`CertificateFileName`/`KeyFileName`/`Subject`(SAN/NotBefore/NotAfter/Thumbprint) for `ReplaceAssets` to actually save |
| `src/ProxyManager.API/Infrastructure/Files/{IFileAssetClient,FileAssetClient}.cs`, `FilesServiceOptions` | Existing HTTP client to `ProxyManager.Files`; currently only registered in `ProxyManager.API/Program.cs`; only has `GetAsync`/`GetContentAsync`/`CommitAsync` — no upload method, since today only the UI uploads bytes directly to `ProxyManager.Files` and the API only ever references an already-staged asset id | Move (not duplicate) into `ProxyManager.Infrastructure` so both `ProxyManager.API` and `ProxyManager` can register the same typed `HttpClient`; add a new `UploadAsync` method so `AggregateCertificateRepository` (T9) can push freshly-issued LE certificate bytes into Files itself |
| `src/ProxyManager/Yarp/DatabaseProxyConfigProvider.cs`, `ProxyHostYarpTranslator.cs` | Confirms `ProxyManager` already has direct EF/Postgres access via `AddProxyManagerInfrastructure()` — no need to go through `ProxyManager.API` for domain/cert data | LE's `IDomainSource`/`ICertificateSource` can inject `IProxyHostRepository`/`ICertificateRepository` (Core) directly |
| `src/ProxyManager.Infrastructure/Data/Migrations/20260726213349_AddCertificateFileAssets.cs` | Most recent migration pattern (clean-break style, no data preservation assumed) | Follow same style for the new `TlsMode`/`Source` migration |
| `systemd/proxymanager.pod` (ports `8443`, `15672`, `5672`, `9000`, `9001` — no `80`), `systemd/proxymanager.container` (mounts `certs/`), `src/ProxyManager/Containerfile` (`EXPOSE 8443` only) | HTTP-01 challenge requires port 80 reachable from the internet; not currently published anywhere | Add `80:80` to pod port mappings and `EXPOSE 80` to Containerfile |
| GitHub `ArchonSystemsInc/LettuceEncrypt-Archon` (v3.0.0, `net10.0`, confirmed via NuGet gallery) | Confirms target-framework compatibility and available extension points: `IDomainSource`, `ICertificateSource`, `ICertificateRepository`, `LettuceEncryptOptions` (`DomainNames`, `AcceptTermsOfService`, `EmailAddress`, `UseStagingServer`, `AllowedChallengeTypes`, `EnableOnDemandCertificateLookup`, `RenewDaysInAdvance`, `FallbackCertificate`, etc.) | Confirms feasibility of the whole approach; `EnableOnDemandCertificateLookup` (default true) means `GetCertificateAsync` per-domain lookups (and thus Files HTTP calls) only happen on cache miss, not per TLS handshake |
| `src/ProxyManager.UI/components/routes/RouteForm.tsx` | No certificate/TLS field exists today; `PUT /proxyhosts/{id}/certificate` has no UI consumer | Add TLS-mode toggle to this form and its request/payload types |
| `tests/ProxyManager.API.Tests/Unit/Fakes/{FakeCertificateRepository,FakeProxyHostRepository}.cs`, `TestCertificates.cs` | Existing xUnit convention: hand-rolled fakes implementing Core repository interfaces directly, no mocking library | New tests for LE sources/repository follow the same fake-based pattern |

- **Open gate:** none — all material ambiguities (cert storage integration, opt-in model, port 80 exposure) were resolved with the user before drafting.

## Tasks

#### T1 — Add `TlsMode` to `ProxyHost`

- **Change:**
  - Add `public enum TlsMode { Manual, LetsEncrypt }` in `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/`.
  - Add `TlsMode` property to `ProxyHost.cs`, defaulted to `Manual` in `Create(...)`, included in `Reconstitute(...)`.
  - Add `SetTlsMode(TlsMode mode)` mutator.
- **Starts at:** `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/ProxyHost.cs`
- **Depends on:** none
- **Tests:** `tests/ProxyManager.Core.Tests/Unit/ProxyHostAggregateTests.cs` (`unit`) — add cases for default `TlsMode`, `SetTlsMode`, and that `Reconstitute` round-trips it.
- **Verify:**
  - Run `dotnet test tests/ProxyManager.Core.Tests/ProxyManager.Core.Tests.csproj --filter "FullyQualifiedName~ProxyHostAggregateTests"`; expect all pass including new `TlsMode` cases.

#### T2 — Persist `TlsMode` (migration, EF config, repository)

- **Change:**
  - Add `tls_mode` (int) column to `ProxyHostRecord.cs` and `ProxyHostConfiguration.cs`, default `0` (`Manual`).
  - Add EF migration `AddProxyHostTlsMode` under `src/ProxyManager.Infrastructure/Data/Migrations/`, following the clean-break style of `20260726213349_AddCertificateFileAssets.cs`.
  - Map `TlsMode` in `PostgresProxyHostRepository.cs` `AddAsync`/`UpdateAsync`/rehydration.
- **Starts at:** `src/ProxyManager.Infrastructure/Data/ProxyHostRecord.cs`, `Configurations/ProxyHostConfiguration.cs`, `Repositories/PostgresProxyHostRepository.cs`
- **Depends on:** T1
- **Tests:** `tests/ProxyManager.API.Tests/Integration/Repositories/PostgresProxyHostRepositoryTests.cs` (`integration`) — add a round-trip case asserting `TlsMode` persists and rehydrates correctly.
- **Verify:**
  - Run `dotnet ef migrations script --project src/ProxyManager.Infrastructure --startup-project src/ProxyManager.API` (or `dotnet build` if script generation needs a startup project not yet configured for this project) to confirm the migration compiles and applies cleanly.
  - Run `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj --filter "FullyQualifiedName~PostgresProxyHostRepositoryTests"`; expect pass.

#### T3 — Add `Source` marker and `ReplaceAssets` to `Certificate`

- **Change:**
  - Add `public enum CertificateSource { Manual, LetsEncrypt }` alongside `CertificateFormat.cs`.
  - Add `Source` property to `Certificate.cs`, set in `Create(...)` (new optional parameter, default `Manual`) and `Reconstitute(...)`.
  - Add `ReplaceAssets(Guid certificateAssetId, Guid? keyAssetId, string certificateFileName, string? keyFileName, CertificateSubjectInfo subject)` mutator enforcing the same `Format == Pfx ⇒ keyAssetId == null` invariant as `Create`, updates `UpdatedAt`. This lets an LE-renewed certificate's `Certificate.Id` (and any `ProxyHost.CertificateId` pointing at it) survive renewal instead of requiring delete+recreate.
- **Starts at:** `src/ProxyManager.Core/AggregatesModel/CertificateAggregate/Certificate.cs`
- **Depends on:** none
- **Tests:** `tests/ProxyManager.Core.Tests/Unit/` — new `CertificateAggregateTests.cs` (`unit`) covering `Source` default/assignment and `ReplaceAssets` invariant enforcement (rejects `Pfx` + non-null `keyAssetId`).
- **Verify:**
  - Run `dotnet test tests/ProxyManager.Core.Tests/ProxyManager.Core.Tests.csproj --filter "FullyQualifiedName~CertificateAggregateTests"`; expect pass.

#### T4 — Persist `Source`/replaced assets (migration, EF config, repository fix)

- **Change:**
  - Add `source` (int, default `0`) column to `CertificateRecord.cs`/`CertificateConfiguration.cs`.
  - Add EF migration `AddCertificateSource`.
  - Fix `PostgresCertificateRepository.UpdateAsync` to also persist `CertificateAssetId`, `KeyAssetId`, `CertificateFileName`, `KeyFileName`, `Subject`/SAN/`NotBefore`/`NotAfter`/`Thumbprint`, `Source` — today it silently drops everything except `Name`/`PassPhrase`/`UpdatedAt`, which would make `ReplaceAssets` (T3) a no-op on save.
- **Starts at:** `src/ProxyManager.Infrastructure/Data/CertificateRecord.cs`, `Configurations/CertificateConfiguration.cs`, `Repositories/PostgresCertificateRepository.cs`
- **Depends on:** T3
- **Tests:** `tests/ProxyManager.API.Tests/Integration/Repositories/PostgresCertificateRepositoryTests.cs` (`integration`) — add a case that calls `ReplaceAssets` then `UpdateAsync`, re-fetches, and asserts the new asset ids/subject/thumbprint were actually persisted (regression guard for the current silent-drop bug).
- **Verify:**
  - Run `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj --filter "FullyQualifiedName~PostgresCertificateRepositoryTests"`; expect pass.

#### T5 — Promote `IFileAssetClient` into shared Infrastructure

- **Change:**
  - Move `IFileAssetClient`, `FileAssetClient`, `FilesServiceOptions` from `src/ProxyManager.API/Infrastructure/Files/` to `src/ProxyManager.Infrastructure/Files/` (namespace update; no behavior change).
  - Update `src/ProxyManager.API/Program.cs` registration to reference the new namespace.
  - Add an `AddFilesClient(IConfiguration)` extension in `ProxyManager.Infrastructure` (mirrors `AddProxyManagerInfrastructure` style) that both `ProxyManager.API` and `ProxyManager` call.
  - Add a new `UploadAsync(string fileName, string contentType, Stream content, CancellationToken ct)` method to `IFileAssetClient`/`FileAssetClient` that POSTs a multipart request to `ProxyManager.Files`' `POST /files` endpoint and returns the new staged asset id. This does not exist today — `IFileAssetClient` currently only has `GetAsync`/`GetContentAsync`/`CommitAsync`; today's cert-upload flow has the UI upload bytes directly to `ProxyManager.Files` and `CreateCertificateHandler` only references an already-staged asset id. `AggregateCertificateRepository` (T9) needs to push freshly-generated LE certificate bytes into Files itself, so this upload path must exist server-side.
- **Starts at:** `src/ProxyManager.API/Infrastructure/Files/IFileAssetClient.cs`, `FileAssetClient.cs`
- **Depends on:** none
- **Tests:** existing `tests/ProxyManager.API.Tests/**` coverage that exercises `IFileAssetClient` (via `CreateCertificateHandlerTests` fakes/integration tests) continues to pass unchanged for the move. Add a new integration test (alongside existing Files-client integration tests, if any exist; otherwise a focused test in `tests/ProxyManager.API.Tests/Integration/`) asserting `UploadAsync` stages bytes in `ProxyManager.Files` and returns an id that `GetContentAsync` can subsequently read back correctly.
- **Verify:**
  - Run `dotnet build ProxyManager.sln`; expect no errors.
  - Run `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj`; expect same pass count as before the move.

#### T6 — Add LettuceEncrypt-Archon package and config

- **Change:**
  - Add `<PackageReference Include="LettuceEncrypt-Archon" Version="3.0.0" />` to `src/ProxyManager/ProxyManager.csproj`.
  - Add `LettuceEncrypt` config section to `src/ProxyManager/appsettings.Development.json` (`UseStagingServer: true`) and a new `src/ProxyManager/appsettings.Production.json` (or extend `appsettings.json`, matching whatever the repo's existing prod-config convention turns out to be) with `UseStagingServer: false`, `AcceptTermsOfService`, `EmailAddress` sourced from environment variables per the existing `Authentication:*` pattern.
  - Add the missing production `Kestrel:Endpoints:Http`/`Https` section (currently only in `appsettings.Development.json`), binding HTTP to `*:80` and HTTPS to `*:8443`.
  - Persist the ACME account key across container restarts (Quadlet containers are ephemeral/replaceable) by pointing the library's account-key storage at the already-mounted `certs/` volume (`~/proxymanager/certs/` on the host, per `systemd/proxymanager.container`), e.g. via `PersistDataToDirectory`/`IAccountStore` file-based persistence, so redeploys don't force re-registration with Let's Encrypt.
- **Starts at:** `src/ProxyManager/ProxyManager.csproj`, `src/ProxyManager/appsettings.Development.json`, `src/ProxyManager/appsettings.json`
- **Depends on:** none
- **Tests:** no automated test (config/package plumbing); verified by T9's startup check.
- **Verify:**
  - Run `dotnet restore ProxyManager.sln`; expect `LettuceEncrypt-Archon` resolves for `net10.0`.

#### T7 — `IDomainSource` implementation

- **Change:**
  - Add `src/ProxyManager/Acme/ProxyHostDomainSource.cs` implementing `IDomainSource.GetDomains(CancellationToken)`: queries `IProxyHostRepository.GetAllAsync()`, filters `TlsMode == LetsEncrypt && IsEnabled`, and returns one `MultipleDomainCert` per host (`OrderedDomains = host.DomainNames`, first domain as CN).
- **Starts at:** `src/ProxyManager/Acme/ProxyHostDomainSource.cs` (new file)
- **Depends on:** T1
- **Tests:** `tests/ProxyManager.API.Tests/` already references `src/ProxyManager/ProxyManager.csproj` via the `extern alias ProxyManagerApp;` convention (see `DatabaseProxyConfigProviderTests.cs`, `ProxyHostChangedHandlerTests.cs`) — add `ProxyHostDomainSourceTests.cs` there using that alias plus the existing `FakeProxyHostRepository`, asserting only `LetsEncrypt`-mode, enabled hosts are returned and domains are grouped correctly per host.
- **Verify:**
  - Run `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj --filter "FullyQualifiedName~ProxyHostDomainSourceTests"`; expect pass.

#### T8 — `ICertificateSource` implementation

- **Change:**
  - Add `src/ProxyManager/Acme/AggregateCertificateSource.cs` implementing `ICertificateSource`:
    - `GetCertificatesAsync`: `ICertificateRepository.GetAllAsync()`, fetch cert+key bytes via `IFileAssetClient.GetContentAsync`, build `X509Certificate2` per row (PFX direct load; PEM combine cert+key via `X509Certificate2.CreateFromPem`), skip/log any row that fails to load rather than throwing.
    - `GetCertificateAsync(domainName, ct)`: same repository call, filtered in-memory to the first `Certificate` whose `Subject.SubjectAlternativeNames` contains `domainName` (case-insensitive), or `null`.
- **Starts at:** `src/ProxyManager/Acme/AggregateCertificateSource.cs` (new file)
- **Depends on:** T5
- **Tests:** `AggregateCertificateSourceTests.cs` in `tests/ProxyManager.API.Tests/` (via `extern alias ProxyManagerApp;`, same as T7) with a fake Core `ICertificateRepository` + fake `IFileAssetClient` asserting: correct cert selected for a matching SAN, `null` for no match, malformed cert row is skipped without throwing.
- **Verify:**
  - Run `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj --filter "FullyQualifiedName~AggregateCertificateSourceTests"`; expect pass.

#### T9 — `ICertificateRepository` (LettuceEncrypt) implementation

- **Change:**
  - Add `src/ProxyManager/Acme/AggregateCertificateRepository.cs` implementing LettuceEncrypt's `ICertificateRepository.SaveAsync(X509Certificate2, ct)`:
    - Export cert to PFX bytes, upload via `IFileAssetClient.UploadAsync` (T5) then `CommitAsync` (mirrors `CreateCertificateHandler`'s existing stage→inspect→commit flow, but originating the upload itself instead of referencing a pre-staged asset).
    - Extract SANs from the issued cert; look up any existing `Certificate` covering the same domain set (via Core `ICertificateRepository.GetAllAsync()` + SAN match) — if found and `Source == LetsEncrypt`, call `ReplaceAssets(...)` (renewal path); otherwise `Certificate.Create(..., source: LetsEncrypt)` (first-issuance path).
    - After save, find the matching `ProxyHost`(s) by domain set and call `AssignCertificate(certificate.Id)` if not already assigned, so the existing manual-assignment metadata stays consistent for LE-issued certs too.
- **Starts at:** `src/ProxyManager/Acme/AggregateCertificateRepository.cs` (new file)
- **Depends on:** T3, T4, T5
- **Tests:** `AggregateCertificateRepositoryTests.cs` in `tests/ProxyManager.API.Tests/` (via `extern alias ProxyManagerApp;`) with fakes for both Core repositories and `IFileAssetClient` covering: first-issuance creates a new `Certificate` with `Source = LetsEncrypt` and assigns it to the matching `ProxyHost`; renewal of an existing LE cert calls `ReplaceAssets` and keeps the same `Certificate.Id`.
- **Verify:**
  - Run `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj --filter "FullyQualifiedName~AggregateCertificateRepositoryTests"`; expect pass.

#### T10 — Wire LettuceEncrypt into `Program.cs`

- **Change:**
  - In `src/ProxyManager/Program.cs`: `builder.Services.AddLettuceEncrypt().PersistCertificatesUsing<AggregateCertificateRepository>()` (or the equivalent `ILettuceEncryptServiceBuilder` registration calls the library exposes) and register `ProxyHostDomainSource`/`AggregateCertificateSource` as their respective interfaces.
  - Bind `LettuceEncryptOptions` from the new `LettuceEncrypt` config section (T6) via `services.Configure<LettuceEncryptOptions>(configuration.GetSection("LettuceEncrypt"))`.
  - Change the HTTPS Kestrel listener setup to call `.UseLettuceEncrypt(applicationServices)`, replacing/removing the now-obsolete static `Kestrel:Https:Sni` dictionary in `appsettings.Development.json`.
- **Starts at:** `src/ProxyManager/Program.cs`
- **Depends on:** T6, T7, T8, T9
- **Tests:** no unit test for DI wiring itself; covered by T13's startup smoke check.
- **Verify:**
  - Run `dotnet build ProxyManager.sln`; expect no errors.

#### T11 — Expose port 80 for ACME HTTP-01

- **Change:**
  - Add `PublishPort=80:80` to `systemd/proxymanager.pod`.
  - Add `EXPOSE 80` to `src/ProxyManager/Containerfile`.
- **Starts at:** `systemd/proxymanager.pod`, `src/ProxyManager/Containerfile`
- **Depends on:** none
- **Tests:** none (infra config); verified by T13's operator-run staging issuance check.
- **Verify:**
  - Run `podman quadlet` config validation (or `./scripts/build-images.sh` per repo convention) if available; otherwise visually confirm the port mapping is present.

#### T12 — UI: TLS mode toggle and certificate source badge

- **Change:**
  - `src/ProxyManager.UI/components/routes/RouteForm.tsx`: add a TLS-mode selector (`Manual` / `Let's Encrypt`) to the form state and payload; update the corresponding `CreateRouteRequest`/`UpdateRouteRequest` types in `lib/proxy-manager-client`.
  - `src/ProxyManager.UI/components/certificates/CertificateCard.tsx`: add a "Source" badge (Manual / Let's Encrypt) next to the existing format badge; disable/hide the manual "Edit"/"Delete" actions (or show a warning) for `Source === "LetsEncrypt"` certs since they're machine-managed.
  - Update the `ProxyHost`/`Certificate` TypeScript types to include the new `tlsMode`/`source` fields.
- **Starts at:** `src/ProxyManager.UI/components/routes/RouteForm.tsx`, `src/ProxyManager.UI/components/certificates/CertificateCard.tsx`
- **Depends on:** T2 (API must expose `tlsMode`), T4 (API must expose `source`)
- **Tests:** co-located Jest/RTL tests — extend `RouteForm.test.tsx` (or add if none exists) to assert the TLS-mode selector renders and submits correctly; extend `CertificateCard.test.tsx` similarly for the Source badge.
- **Verify:**
  - Run `cd src/ProxyManager.UI && npm test`; expect pass including new cases.

#### T13 — API surface for `TlsMode`/`Source` + end-to-end smoke check

- **Change:**
  - Add `TlsMode` to `ProxyHostDto`, `CreateProxyHostRequest`/`UpdateProxyHostRequest`, `CreateProxyHostCommand`/`UpdateProxyHostCommand`, and their handlers.
  - Add `Source` (read-only) to `CertificateDto` and `GetCertificatesHandler`'s `MapToDto`.
  - Manual/operator verification (not automated in CI): point `ProxyManager` at a real publicly-resolvable test domain, set `UseStagingServer: true`, start the app, confirm a staging certificate is issued (check logs for LettuceEncrypt issuance success) and served over TLS for that domain; confirm a `Certificate` row appears with `Source = LetsEncrypt` and `ProxyHost.CertificateId` is auto-assigned.
- **Starts at:** `src/ProxyManager.API/Endpoints/ProxyHostEndpoints.cs`, `Core/DTOs/ProxyHostDto.cs`, `Core/DTOs/CertificateDto.cs`, `Endpoints/CertificateEndpoints.cs`
- **Depends on:** T2, T4, T7, T8, T9, T10, T11
- **Tests:** extend `tests/ProxyManager.API.Tests/Unit/Handlers/{CreateProxyHostHandlerTests,UpdateProxyHostHandlerTests}.cs` for `TlsMode` passthrough; extend `GetCertificatesHandlerTests` (or add if none exists) for `Source` passthrough.
- **Verify:**
  - Run `dotnet test ProxyManager.sln`; expect full solution pass.
  - Perform the manual staging-issuance check above; expect a successfully issued and served staging certificate, logged with no errors.

## Final acceptance

- **Checks:**
  - `dotnet build ProxyManager.sln` succeeds.
  - `dotnet test ProxyManager.sln` passes (all Core/API/Infrastructure test projects, including new tests from T1–T4, T7–T9, T13).
  - `cd src/ProxyManager.UI && npm test` passes.
  - `dotnet ef migrations` for the two new migrations apply cleanly against a fresh Postgres instance.
- **End state:** `ProxyHost` records can be flagged `TlsMode: LetsEncrypt`; their domains are automatically issued and renewed via Let's Encrypt (staging in dev, production with explicit `AcceptTermsOfService`/`EmailAddress` env config); issued certificates are visible in the existing certificate UI tagged `Source: Let's Encrypt`; Kestrel serves the correct certificate for any domain covered by any stored certificate (manual or LE), closing the previously-unused `CertificateId` gap; port 80 is reachable for ACME HTTP-01 challenges in the deployed Quadlet.
- **Deferrals or blockers:** DNS-01/wildcard support, distributed multi-instance challenge/cert-store sharing, and admin force-renew/revoke tooling are explicitly deferred (see Non-goals). The manual staging-issuance check in T13 requires a real internet-reachable domain and cannot be run in CI — it is an operator verification step, not an automated gate.
