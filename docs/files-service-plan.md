# ProxyManager.Files — asset upload/storage service backed by RustFS

## Context

The certificate feature is a shell around a storage layer that doesn't exist:

- `certificates` table stores `certificate_path` / `key_file_path` — **filesystem paths that nothing ever writes**.
- `CertificateForm.tsx` has a real `<input type="file">` that discards the bytes and submits `file.name`.
- `lib/certificate-store.ts` is an in-memory mock; there are no `app/api/certificates/**` BFF routes.
- `DeleteCertificateHandler` is the only code that touches the filesystem, deleting paths that were never created.

This plan builds the missing layer as a **separate deployable**, `src/ProxyManager.Files` (`West94.ProxyManager.Files`), backed by RustFS over the S3 API and proxied through YARP at `/api/files/**`. It is generic from day one — certificates are the first asset type, not the only one.

**Decisions locked by the user:**
1. Separate service (not a library, not endpoints bolted onto ProxyManager.API).
2. Proxy-through uploads — the service validates then streams to RustFS. RustFS is never browser-reachable.
3. Dynamic TLS/SNI wiring is **out of scope** (see Deferred).
4. Passphrase encryption at rest is **out of scope** (see Deferred).

---

## Pre-existing breakage in the blast radius

These are already broken and will block this work. Fixed where noted.

| # | Problem | Handling |
|---|---|---|
| 1 | **Pod DNS can't work as configured.** `proxymanager.container`, `-postgresql`, `-rabbitmq` use `Pod=proxymanager.pod` — pod members share one netns and reach each other on `localhost`, not by name. But `proxysettings.json` targets `http://proxymanager-api:5001/`. Meanwhile `proxymanager-ui.container` uses `Network=` (not the pod), so it *is* name-resolvable but isn't on pod-localhost. | Fix in Phase 5 — pick one model (recommend: everything on `proxymanager.network`, drop the pod, keep `PublishPort` on the proxy only). |
| 2 | **No `proxymanager-api.container` quadlet exists at all**, yet proxysettings and the UI both target it. | Add in Phase 5. |
| 3 | `systemd/proxymanager-rustfs.container` is **untracked**, has `RUSTFS_ADRESS=9000` (typo; and a port where an address belongs), no `RUSTFS_VOLUMES`, and an `ExecStartPre` using `&&` — systemd doesn't invoke a shell, so `&&` is passed as literal argv and the line fails. | Fix + commit in Phase 5. Correct vars: `RUSTFS_ADDRESS=:9000`, `RUSTFS_VOLUMES=/data`, `RUSTFS_CONSOLE_ENABLE=true`. |
| 4 | `build-images.sh` tags (`west94.com/proxy-manager`, `proxy-api`) **match no quadlet** (`west94.com/proxymanager:1.0.0-alpha`), and it never builds the UI. | Fix in Phase 5. |
| 5 | No `.containerignore`; all Containerfiles `COPY . ./` from repo root, dragging `src/ProxyManager.UI/node_modules` into every .NET image. A third .NET image makes this three times worse. | Add in Phase 5. |
| 6 | `deploy-vm.sh` config glob is quoted (`"$REPO_ROOT/proxysettings.*.json"` — never expands) and points at the repo root; the files live in `src/ProxyManager/`. `systemd/.env` doesn't exist, so `EnvironmentFile=.env` silently yields no Postgres password on a fresh deploy. | One-line fixes, flagged, independent of this work. |
| 7 | `proxysettings.Development.json` `apiRoute` has **no `BearerToken` transform** and points at `https://localhost:5001`. Uploads will work in prod and 401 in dev. | Fix in Phase 5. |
| 8 | UI is already broken against the current API: `types/index.ts` has `ProxyCertificate { certificatePath, keyPath }` on `ProxyHost` but the API returns `certificateId: Guid?`; `proxy-manager-client.ts` still sends `certificatePath`/`certificateKeyPath` on route create/update, which the API no longer accepts. | Fix in Phase 6. |

---

## Phase 1 — Storage abstraction + RustFS spike

**New project** `src/ProxyManager.Files/ProxyManager.Files.csproj` — `net10.0`, `AssemblyName`/`RootNamespace` = `West94.ProxyManager.Files`, `UserSecretsId`. Add to `ProxyManager.sln`. No central package management exists, so pin versions in the csproj like the other projects.

### NuGet: `AWSSDK.S3`, not the Minio SDK

AWSSDK.S3 gives `TransferUtility` (automatic multipart), `GetPreSignedURL`, versioning, byte-range GETs — and it's the client every S3-compatible server, RustFS included, tests against. Minio's .NET SDK is narrower and has churned across 4→5→6→7.

**Hazard that will bite first:** AWS SDK v4 defaults `RequestChecksumCalculation = WhenSupported`, adding `x-amz-checksum-crc32` / `aws-chunked` trailers to every `PutObject`. Many S3-compatible servers reject these. Config must be:

```csharp
new AmazonS3Config {
    ServiceURL = options.ServiceUrl,
    ForcePathStyle = true,                    // RustFS has no vhost-style DNS
    AuthenticationRegion = options.Region,    // SigV4 needs a region string
    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
}
```

> **Acceptance gate — do this before writing any endpoint code.** Throwaway spike: `PutObject → GetObject → HeadObject → DeleteObject → GetPreSignedURL` against a real `rustfs/rustfs:latest` container. Also verify that `RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY` are actually honoured in container mode — [rustfs#1058](https://github.com/rustfs/rustfs/issues/1058) reports them being ignored, which would change the credential story entirely.

### `src/ProxyManager.Files/Storage/IObjectStore.cs`

```csharp
Task PutAsync(string key, Stream content, long contentLength, string contentType,
              IReadOnlyDictionary<string,string>? metadata, CancellationToken ct);
Task<ObjectStoreDownload?> GetAsync(string key, CancellationToken ct);   // null == not found
Task<ObjectStoreStat?>     StatAsync(string key, CancellationToken ct);
Task  DeleteAsync(string key, CancellationToken ct);                     // idempotent
Task  CopyAsync(string sourceKey, string destKey, CancellationToken ct);
Uri   CreatePresignedUrl(string key, HttpMethod method, TimeSpan ttl);   // sync: pure local signing
```

- `StatAsync` rather than `ExistsAsync` — same `HeadObject` round-trip, but returns `ObjectStoreStat(ContentLength, ContentType, ETag, LastModified)` which the download endpoint needs for conditional GETs. `Exists` becomes `StatAsync(...) is not null`.
- `CopyAsync` is **required**, not optional — it's how staging→committed promotion happens server-side without re-uploading (Phase 3).
- `CreatePresignedUrl` ships as the seam for large assets later but **no HTTP endpoint exposes it** — a presigned URL is useless unless RustFS is browser-reachable, which contradicts decision #2. Having it on the interface means enabling it later isn't an abstraction rewrite.
- `ObjectStoreDownload(Stream Content, ObjectStoreStat Stat) : IAsyncDisposable`; the endpoint pipes `Content` straight to the response body.

Implementation: `Storage/S3ObjectStore.cs` — `sealed class S3ObjectStore(IAmazonS3 s3, IOptions<ObjectStorageOptions> options) : IObjectStore`.

### Options + credentials

`src/ProxyManager.Files/Options/ObjectStorageOptions.cs`, section `"ObjectStorage"`: `ServiceUrl`, `Bucket` (default `proxymanager`), `Region`, `AccessKey`, `SecretKey`, `ForcePathStyle`, `AutoCreateBucket`, `PresignedUrlTtl`. Registered in `src/ProxyManager.Files/Infrastructure/ServiceCollectionExtensions.cs` (`AddFilesServices`), mirroring `src/ProxyManager.API/Infrastructure/ServiceCollectionExtensions.cs`.

Credentials come from the **same two Podman secrets the rustfs quadlet already uses**, retargeted to ASP.NET-shaped names — no code, no `{{TOKEN}}` interpolation:

```ini
Secret=rfs-access-key,type=env,target=ObjectStorage__AccessKey
Secret=rfs-secret-key,type=env,target=ObjectStorage__SecretKey
```

Bucket bootstrap: `Services/BucketBootstrapHostedService.cs` (`IHostedService`, `HeadBucket` → `PutBucket` if absent), gated on `AutoCreateBucket`. Mirrors the pattern of `src/ProxyManager.API/Services/DatabaseMigrationService.cs`.

---

## Phase 2 — Asset model and persistence

### Key scheme — single bucket, two prefixes

```
staging/{uploadId:N}/{sanitizedFilename}       # uncommitted, sweeper-eligible
{assetType}/{assetId:N}/{sanitizedFilename}    # committed
```

- **No tenant segment.** There is no tenant concept in this codebase and asset IDs are already globally-unique GUIDs. A leading segment you don't need becomes a full key-scheme migration to remove.
- `assetType` is a lowercase string validated against an allowlist so it can't be path-traversed.
- Filename is sanitized (strip separators, `..`, control chars; NFC-normalize; truncate to 200; fall back to `asset.bin`) and exists only for `Content-Disposition` and console browsing. **All lookups go by asset ID.**
- One bucket, not per-type: per-type buckets mean N bootstrap steps and a bucket decision on every new asset type — exactly the friction this service exists to remove. Prefix-scoped policies give the same isolation later. Bucket versioning off initially.

### Files owns its own metadata table

Rationale: the orphan sweeper needs to query `Status = Staged AND CreatedAt < cutoff` — a stateless service would have to `ListObjects` the whole staging prefix and infer age from `LastModified`. And "metadata in the caller's DB" would force Files to reference `ProxyManager.Core` + `.Infrastructure`, making the generic asset service a satellite of the cert domain on day one. The caller still stores its own reference (`certificates.certificate_asset_id`, plain `uuid`, no FK — same cross-aggregate precedent as `proxy_hosts.certificate_id`).

**Same Postgres instance, separate schema — this is the sharpest hazard in the plan:**

> `DatabaseMigrationService` runs `MigrateAsync()` on `ProxyManagerDbContext` against the default `__EFMigrationsHistory`. If `FilesDbContext` migrates against the same database with the default history table, the two services will each treat the other's migrations as unknown and corrupt each other's state.

Mandatory, **written before the first migration is generated** (retrofitting is manual SQL):

```csharp
modelBuilder.HasDefaultSchema("files");
// and at registration:
options.UseNpgsql(cs, o => {
    o.MigrationsAssembly(typeof(FilesDbContext).Assembly.FullName);
    o.MigrationsHistoryTable("__ef_migrations_history", "files");
});
```

### `FileAsset`

Plain sealed class in `src/ProxyManager.Files/Assets/FileAsset.cs` — **not** `Core.SeedWork.Entity`; Files does not reference Core for its own model.

| Property | Column | Notes |
|---|---|---|
| `Id` | `id uuid PK` | |
| `AssetType` | `asset_type varchar(64)` | `certificate`, … |
| `FileName` | `file_name varchar(256)` | sanitized |
| `ContentType` | `content_type varchar(128)` | **server-determined**, never client-supplied |
| `SizeBytes` | `size_bytes bigint` | |
| `Sha256` | `sha256 char(64)` | lowercase hex — integrity + dedupe |
| `StorageKey` | `storage_key text` | full key incl. prefix |
| `Status` | `status int` | `Staged=0, Committed=1, Deleted=2` |
| `OwnerType` / `OwnerId` | `owner_type varchar(64) NULL`, `owner_id uuid NULL` | set at commit |
| `UploadedBy` | `uploaded_by varchar(256)` | actor `sub` claim |
| `CreatedAt` / `CommittedAt` | `created_at`, `committed_at NULL` | `timestamptz` |

Indexes: `(owner_type, owner_id)` for cascade-delete lookups, `(status, created_at)` for the sweeper.

Persistence mirrors the existing split exactly — `Data/FileAssetRecord.cs`, `Data/Configurations/FileAssetConfiguration.cs`, `Repositories/PostgresFileAssetRepository.cs` with private `ToDomain`/`ToRecord`, per `src/ProxyManager.Infrastructure/Repositories/PostgresCertificateRepository.cs`. snake_case columns throughout.

---

## Phase 3 — HTTP contract

`src/ProxyManager.Files/Endpoints/FileEndpoints.cs`, `MapGroup("/files").WithTags("Files").RequireAuthorization()`, `TypedResults` + `Results<Ok<T>, ProblemHttpResult>` unions per `src/ProxyManager.API/Endpoints/CertificateEndpoints.cs`.

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/files` (multipart) | 201 `Created<FileAssetDto>` | 400, 413, 415 |
| POST | `/files/{id:guid}/commit` | 200 `Ok<FileAssetDto>` | 404 |
| GET | `/files/{id:guid}` | 200 `Ok<FileAssetDto>` | 404 |
| GET | `/files/{id:guid}/content` | 200 stream | 404 |
| GET | `/files?ownerType=&ownerId=&page=&pageSize=` | 200 `PagedResult<FileAssetDto>` | — |
| DELETE | `/files/{id:guid}` | 204 | 404 |

`FileAssetDto` (`Contracts/FileAssetDto.cs`) **deliberately excludes `StorageKey`** — leaking it invites callers to bypass the service, which is exactly the `CertificatePath` mistake being undone. `PagedResult<T>` is duplicated into `Contracts/PagedResult.cs` rather than referencing `Core.DTOs` (ten lines beats a cross-service type dependency; note it in the file).

### Two-phase upload: staged → committed

`POST /files` writes to `staging/…`, returns `Status: Staged`. `POST /files/{id}/commit { ownerType, ownerId }` server-side-copies to the committed key, deletes the staging object, sets `Status`/`OwnerType`/`OwnerId`/`CommittedAt`. A hosted sweeper (`Services/StagedAssetSweeper.cs`) deletes `Staged` assets older than `StagingTtl` (default 30 min) **and only where `OwnerId is null`**, plus their blobs.

Rejected alternative — commit-on-upload with best-effort caller rollback: it means the API deletes blobs in a `catch` block, which is precisely the failure mode (process dies between DB write and cleanup) that produces orphans. The sweeper is needed as a backstop either way, so staging is nearly free and makes cleanup deterministic.

**`commit` is idempotent** — committing an already-committed asset returns 200, not 409. This is what covers the one real gap in the failure analysis (see Phase 4).

### Streaming and an honest size limit

**Do not bind `IFormFile` and do not call `ReadFormAsync()`** — both buffer the whole body. Read the raw body via `MediaTypeHeaderValue.Parse(Request.ContentType).Boundary` → `new MultipartReader(boundary, Request.Body)` → `ReadNextSectionAsync()` → `section.AsFileSection()`.

The honest constraint: you cannot SHA-256, magic-byte-sniff, and X509-parse a stream you read once and hand straight to S3. Something must hold the bytes. So:

- **Now (validated small assets):** cap at `MaxUploadBytes` (default **10 MB**, options-bound). Copy through `IncrementalHash` with a hard byte counter that aborts at the cap, then `PutAsync` from the resulting seekable stream. Peak memory is `cap × concurrency`; certs are single-digit KB. This is "buffer with a proven ceiling," not "avoid buffering."
- **Later (large assets):** presigned PUT direct to storage, where the service never sees the bytes and therefore cannot validate them. That trade — validation *or* size — is inherent. Document it.

Set **all three** limits (any one alone gets silently overridden): `KestrelServerOptions.Limits.MaxRequestBodySize`, per-endpoint `RequestSizeLimitAttribute`, and your own counter in the copy loop returning `413` + `application/problem+json` (Kestrel's own limit produces a connection reset — terrible client experience).

### Content-type allowlist — `Validation/UploadContentValidator.cs`

Three gates; never trust the client header or the extension alone:
1. Extension allowlist per `assetType` (`certificate` → `.pfx .p12 .pem .crt .cer .key`), options-bound.
2. Magic bytes: PEM must start `-----BEGIN`; PKCS#12/DER must start `0x30 0x82`.
3. Server-assigned content type derived from (1)+(2), stored in `content_type`. The client's header is logged and discarded.

Deeper X509 semantics do **not** live here (Phase 4).

### Auth, and a CSRF hole that already exists

Browser calls use the **same Authentik JWT bearer as ProxyManager.API** — identical `AddJwtBearer` reading `Authentication:Authority`/`:Audience`, so the token YARP injects validates against both services. Actor extraction (`NameIdentifier ?? "sub" ?? "unknown"`) goes in `Endpoints/ClaimsPrincipalExtensions.cs` rather than being copy-pasted six times.

> **CSRF:** minimal APIs auto-apply antiforgery only to endpoints binding `IFormFile`; reading the body manually means it won't fire. But the real hole is one hop upstream and **already exists today** for every mutating `/api/**` request: the browser sends only a cookie, and YARP's `BearerToken` transform *mints* the credential — a textbook cookie-to-bearer bridge. A cross-site `<form method=post enctype=multipart/form-data>` would be sent with the session cookie and YARP would attach a valid token. This plan doesn't create the hole, but an unauthenticated-body upload endpoint makes it far more attractive.
>
> Fix at the YARP route (Phase 5) where the cookie→bearer conversion happens — require a custom header the route can't be reached without:
> ```json
> "Match": { "Path": "/api/files/{**catch-all}",
>            "Headers": [ { "Name": "X-Requested-With", "Values": ["proxymanager-ui"], "Mode": "ExactHeader" } ] }
> ```
> HTML forms cannot set custom headers, and a cross-origin `fetch` that sets one triggers a preflight we never answer. Extending the same match to `apiRoute` is recommended as a **separate** follow-up — it's a pre-existing finding, not a silent fix.

**Service-to-service (API → Files)** is the awkward part — a Wolverine handler has no `HttpContext` and no user token. Forwarding the caller's bearer is rejected outright (tokens on Wolverine messages get persisted to the durable inbox and republished to RabbitMQ — credentials in a message body is a bad precedent). Target state is OAuth2 client-credentials against Authentik with a dedicated scope, but that blocks on someone's Authentik admin console. **Ship a shared service token from a Podman secret, validated by a second authentication scheme, behind an `IFilesServiceTokenProvider` seam** — swapping to client-credentials later touches one file. This is a deliberate temporary shortcut. Phase 4's event-driven deletion removes the most frequent service-to-service call, leaving only the content fetch during cert creation.

---

## Phase 4 — Certificate integration

### Domain (`src/ProxyManager.Core/AggregatesModel/CertificateAggregate/Certificate.cs`)

```csharp
Create(string name, CertificateFormat format,
       Guid certificateAssetId, Guid? keyAssetId,
       string? passPhrase, CertificateSubjectInfo subject)
```

- The existing invariant migrates cleanly: *"PFX bundles the key; `KeyAssetId` must be null"* — same rule, new field.
- New `CertificateSubjectInfo` value object in the same folder: `Subject`, `IReadOnlyList<string> SubjectAlternativeNames`, `NotBefore`, `NotAfter`, `Thumbprint`.
- New denormalized `CertificateFileName` / `KeyFileName` on the aggregate so the cert list renders without N+1 calls into Files. Filenames are immutable post-upload, so no staleness risk.
- `Rename` / `UpdatePassPhrase` unchanged. Asset IDs are immutable — replacing bytes means a new aggregate, consistent with the delete-and-recreate rule in `docs/certificate-api-plan.md`.

`CertificateDto` gains `CertificateAssetId`, `KeyAssetId`, filenames, and the subject fields; loses `CertificatePath`/`KeyFilePath`. `PassPhrase` stays excluded.

### EF migration on `ProxyManagerDbContext`

Clean break — no dual-write, no compat columns (`docs/certificate-api-plan.md` established there are no live cert rows). Drop `certificate_path`, `key_file_path`; add `certificate_asset_id uuid NOT NULL`, `key_asset_id uuid NULL`, `certificate_file_name`, `key_file_name`, `subject`, `subject_alternative_names jsonb`, `not_before`, `not_after`, `thumbprint`.

> **Sequencing hazard:** adding `NOT NULL` columns without defaults fails on a non-empty table. Confirm the target DB is empty, or do nullable → backfill → alter.

SANs as `jsonb` (`.HasColumnType("jsonb")`) — Npgsql maps `List<string>` natively and it beats `text[]` for future querying.

### X509 validation lives in `ProxyManager.Core`

`src/ProxyManager.Core/Certificates/X509CertificateInspector.cs` — pure static `Inspect(ReadOnlySpan<byte> cert, ReadOnlySpan<byte>? key, CertificateFormat, string? passPhrase) → CertificateInspectionResult`, using `System.Security.Cryptography.X509Certificates` so Core stays dependency-free.

Not in Files: "does this private key match this certificate" is cert-domain knowledge; putting it there means the next asset type inherits a cert parser. Not inline in the API handler: untestable without a web host, and unreachable from `ProxyManager`, which will need the identical parse for SNI later. In Core it's unit-testable in `tests/ProxyManager.Core.Tests` against fixture bytes with no container, host, or network.

Responsibilities: parse PEM vs PKCS#12, verify the key matches the cert's public key, verify the passphrase decrypts, extract subject/SANs/validity/thumbprint, and **warn-not-fail on already-expired certs** (uploading a soon-to-be-renewed cert is legitimate). Throws the existing `CertificateValidationException`, so `CertificateEndpoints.cs` maps it to 400 with no change to its catch blocks.

### Flow, and the ordering problem

1. Browser `POST /api/files` (×1, or ×2 for PEM) → `{ id, fileName, sizeBytes, sha256 }`, `Staged`.
2. Browser `POST /api/certificates { name, format, certificateAssetId, keyAssetId?, passPhrase? }`.
3. `CreateCertificateHandler`:
   a. `GET /files/{id}/content` for each asset via `IFileAssetClient` (typed `HttpClient`, service credentials).
   b. `X509CertificateInspector.Inspect(...)` → on failure, 400 with nothing persisted.
   c. `Certificate.Create(...)` → `repository.AddAsync`.
   d. `POST /files/{id}/commit { ownerType: "certificate", ownerId: cert.Id }` per asset.
   e. Return `(CertificateDto, CertificateCreatedEvent)`.

| Fails at | Outcome |
|---|---|
| Upload | Nothing exists. Clean. |
| (b) validation | No DB row; blobs `Staged` → swept. Clean. |
| (c) DB write | Same. Clean. |
| (d) commit, or crash between (c) and (d) | DB row points at `Staged` assets — **the one real gap**. |

Covered by: sweeper only deletes `Staged` **with `OwnerId is null`**, `commit` is idempotent, and a startup reconciliation pass in the API re-drives commit for certs whose assets are still `Staged`. No extra scheduling needed.

### Deletion becomes event-driven

`src/ProxyManager.API/Handlers/DeleteCertificateHandler.cs` loses `TryDeleteFile`, `System.IO.File`, and its `ILogger` — it shrinks to find → remove → return event.

`CertificateDeletedEvent` already publishes to the durable `certificates` fanout exchange declared in `src/ProxyManager.API/Program.cs`, and **nothing consumes it today**. Files subscribes:

```csharp
// src/ProxyManager.Files/Integrations/CertificateAssetCleanupHandler.cs
public async Task Handle(CertificateDeletedEvent e, IFileAssetService assets, CancellationToken ct)
    => await assets.DeleteByOwnerAsync("certificate", e.Id, ct);
```

Why not a synchronous `DELETE /files/{id}`: Wolverine's durable inbox retries if RustFS is down, whereas a synchronous call either fails the user's delete or silently leaks the blob (which is what the current try/catch-and-log does). It also means the API needs no Files credentials or liveness to delete a certificate.

Trade-off, stated plainly: Files gains a reference to `ProxyManager.Core` (event type) and `WolverineFx.RabbitMQ` — a deliberate exception to "Files is domain-agnostic," confined to `Integrations/` and gated by the existing `RabbitMQ:Enabled` flag so tests and dev runs work without a broker. Duplicating the event contract instead would trade a project reference for silent-breakage-on-rename, which is worse. Files' `Program.cs` needs `opts.ListenToRabbitQueue("files-certificate-cleanup").BindExchange("certificates")` with `AutoProvision()`.

Consequence: deletion is now eventually consistent. If RabbitMQ is down, blobs outlive their certificate until it recovers. A `GET /files?ownerType=certificate` reconciliation report is a sensible ops follow-up.

---

## Phase 5 — Deployment

- `src/ProxyManager.Files/Containerfile` mirroring `src/ProxyManager.API/Containerfile`; entry `West94.ProxyManager.Files.dll`.
- **`.containerignore`** at repo root excluding `**/node_modules`, `**/bin`, `**/obj`, `.git` — fixes issue #5 for all three images.
- `systemd/proxymanager-files.container` — image `west94.com/proxymanager-files:1.0.0-alpha`, `EnvironmentFile=.env`, the two `ObjectStorage__*` secrets, `Requires=proxymanager-rustfs.service` + `proxymanager-postgresql.service`.
- `systemd/proxymanager-api.container` — **missing today** (issue #2); add it.
- Fix `systemd/proxymanager-rustfs.container`: `RUSTFS_ADDRESS=:9000`, `RUSTFS_VOLUMES=/data`, `RUSTFS_CONSOLE_ENABLE=true`; split `ExecStartPre` into two lines (systemd doesn't run a shell, so `&&` fails); **git-add it** (currently untracked). Note `chown 1000:1000` is wrong under rootless Podman — needs `podman unshare`.
- **Settle the networking model** (issue #1). Recommend moving everything to `Network=proxymanager.network` and dropping `Pod=`, so service names resolve as `proxysettings.json` and the UI env already assume. If the pod is kept instead, `PublishPort` must be removed from every member (Podman rejects it) and all inter-service URLs become `localhost:port`. **Do not add `Pod=` to the rustfs unit without deleting its `PublishPort` lines in the same edit.**
- YARP: add `filesRoute` for `/api/files/{**catch-all}` → the Files cluster, with `PathRemovePrefix`, `BearerToken`, `RequestHeaderRemove: Cookie`, and the `X-Requested-With` header match. Add a `BearerToken` transform to `apiRoute` in `proxysettings.Development.json` (issue #7).
- `scripts/build-images.sh`: fix tags to match the quadlets, add the Files and UI images (issue #4). `scripts/deploy-vm.sh`: fix the quoted glob and source directory (issue #6).

---

## Phase 6 — UI

> `src/ProxyManager.UI/AGENTS.md` warns this Next.js version has breaking changes and to read `node_modules/next/dist/docs/` before writing code. Do that first — don't write App Router code from memory.

- **`types/index.ts`** — replace the mock `Certificate` with the real shape (`id`, `name`, `format`, `certificateAssetId`, `keyAssetId?`, filenames, subject fields, timestamps); add `FileAsset`. Delete the stale `ProxyCertificate` and change `ProxyHost.certificate` → `certificateId?: string` (issue #8). **Note the casing trap:** the API round-trips `Enum.ToString()`, so format comes back `"Pfx"|"Pem"`, not the UI's current `"PFX"|"PEM"` — `ACCEPT_BY_FORMAT` lookups and `format === "PEM"` comparisons will silently fail. Normalize at the client boundary.
- **`lib/proxy-manager-client.ts`** — add `listCertificates`/`getCertificate`/`createCertificate`/`updateCertificate`/`deleteCertificate` and `uploadFileAsset(session, file, assetType)`. The existing `apiFetch` hardcodes `Content-Type: application/json`, which **corrupts multipart** (the boundary is lost) — `uploadFileAsset` needs a path that omits the header entirely and lets `fetch` set it from the `FormData`. While here, drop `certificatePath`/`certificateKeyPath` from the route request types.
- **New BFF route handlers** under `app/api/certificates/**` and `app/api/files/**`, mirroring the existing `app/api/routes/**`. The upload handler must stream, not buffer.
- **Delete `lib/certificate-store.ts` and `lib/mock-certificates.ts`**; rewire `CertificateListClient.tsx`, `NewCertificateClient.tsx`, `CertificateDetailClient.tsx` to real calls.
- **`components/certificates/CertificateForm.tsx`** — actually submit the `File` objects (upload first, then create with the returned asset IDs), plus upload progress and error states. Surface the expiry warning from the inspector.

---

## Phase 7 — Tests

- **`tests/ProxyManager.Core.Tests`** — `X509CertificateInspector` against committed fixture bytes (self-signed PEM + PFX, matched and mismatched key pairs, wrong passphrase, expired cert). No container, no host. `Category=Unit`, so CI runs it.
- **New `tests/ProxyManager.Files.Tests`** — xUnit v3 on Microsoft.Testing.Platform, `OutputType=Exe`, `UseMicrosoftTestingPlatformRunner=true`, matching the existing test csprojs. Hand-written fakes in `Unit/Fakes/` (no Moq — repo convention). Unit-test key derivation, filename sanitization, `UploadContentValidator`, the size-cap abort, and sweeper eligibility.
- **Integration** — extend `Helpers/TestWebAppFactory.cs`'s pattern: `Testcontainers.PostgreSql` plus a **generic `Testcontainers` container for `rustfs/rustfs:latest`** (no dedicated module exists; use `ContainerBuilder` with a port-9000 wait strategy). Set `RabbitMQ:Enabled=false` and reuse `Helpers/TestJwtFactory.cs`. Mark `[Trait("Category", "Integration")]` — CI filters these out, matching existing practice.
- **UI** — Jest tests for `CertificateForm` submitting real files and for the client's multipart path; a Playwright spec under `tests/e2e/` for upload → list → delete. There are currently **no** certificate tests on either side.

---

## Build order (stacked feature branches, per `docs/certificate-api-plan.md`)

| # | Branch | Contents | Gate |
|---|---|---|---|
| 0 | *(spike, not merged)* | RustFS/AWSSDK compatibility spike | Phase 1 acceptance gate passes |
| 1 | `feature/files-storage-abstraction` | Phase 1 | Unit tests green |
| 2 | `feature/files-asset-model` | Phase 2 | Migration applies to a clean DB in the `files` schema |
| 3 | `feature/files-http-api` | Phase 3 | Integration test uploads/commits/downloads against a RustFS container |
| 4 | `feature/certificate-assets` | Phase 4 | Cert create/delete round-trips end to end |
| 5 | `feature/files-deployment` | Phase 5 | `deploy-vm.sh` brings the full stack up |
| 6 | `feature/certificate-ui` | Phase 6 | Real upload works in the browser |

Phase 7 tests land with the phase they cover, not as a separate branch.

---

## Verification

```bash
# Spike gate (phase 0) — must pass before anything else
podman run --rm -p 9000:9000 -e RUSTFS_ADDRESS=:9000 -e RUSTFS_VOLUMES=/data \
  -e RUSTFS_ACCESS_KEY=test -e RUSTFS_SECRET_KEY=testtest docker.io/rustfs/rustfs:latest

dotnet build ProxyManager.sln
dotnet test ProxyManager.sln
dotnet test tests/ProxyManager.Files.Tests/ProxyManager.Files.Tests.csproj

cd src/ProxyManager.UI && npm test && npm run build && npm run test:e2e
```

End-to-end (after Phase 6): run proxy + API + Files + RustFS + Postgres, sign in through Authentik, upload a real `.pfx` at `/certificates/new`, confirm (a) 201 with a populated subject/SAN/expiry, (b) the object exists under `certificate/{id}/` in the RustFS console at :9001 and **not** under `staging/`, (c) deleting the cert removes the blob via the RabbitMQ path, (d) an oversized upload returns 413 as `problem+json` rather than a connection reset, (e) a `.txt` upload returns 415.

---

## Deferred (explicitly out of scope)

1. **Dynamic TLS/SNI.** `CLAUDE.md` claims the proxy loads certs from `certs/` via SNI callbacks — it does not. `src/ProxyManager/Program.cs` has zero TLS code; `appsettings.Development.json` has a **static** Kestrel `Sni` map, and `ProxyHostYarpTranslator.Translate` ignores `host.CertificateId` entirely. **Uploading a certificate still has no effect on what the proxy serves.** Closing this means a `ServerCertificateSelector` backed by a cache fed from Files, invalidated by the `certificates` exchange. Follow-up.
2. **Passphrase encryption.** `pass_phrase` remains plaintext `text` in Postgres. Anyone with DB read access gets every private-key passphrase. Recommend ASP.NET Data Protection with a keyring on a mounted volume when picked up.
3. **OAuth2 client-credentials** for service-to-service auth, replacing the shared token from Phase 3.
4. **Presigned uploads** for large assets, and the RustFS networking decision that enables them.
5. **CSRF hardening of `apiRoute`** — the cookie→bearer bridge affects every mutating `/api/**` endpoint today, not just uploads.
6. **`CLAUDE.md` corrections** — stale package versions, the "Core/Infrastructure are scaffolding only" claim, the dead `data-model.md` link, and the SNI claim in #1.
7. **Committed secrets** — `systemd/.env` (Postgres password), repo-root `.env` (live `GITHUB_PAT`), RabbitMQ `definitions.json` hash, and the TLS private key at `src/ProxyManager/certs/`. Unrelated to this work but worth rotating.

Sources: [RustFS](https://github.com/rustfs/rustfs) · [RustFS versioning](https://docs.rustfs.com/features/versioning/) · [RustFS Docker install](https://docs.rustfs.com/installation/docker/) · [rustfs#1058 — env credentials in Docker](https://github.com/rustfs/rustfs/issues/1058)
