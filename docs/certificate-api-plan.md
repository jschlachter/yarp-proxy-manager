# Plan: Certificate Aggregate Root + REST API

## Context

`ProxyCertificate` is currently a value object embedded on `ProxyHost` (three nullable columns on `proxy_hosts`). This prevents certificates from being managed independently, shared across hosts (e.g., wildcard certs), or tracked with their own lifecycle. The goal is to promote `Certificate` to a first-class aggregate root with full CRUD endpoints, while `ProxyHost` retains only a nullable FK (`CertificateId`).

**Cert format rules:**
- **PFX**: `CertificatePath` + optional `PassPhrase`. `KeyFilePath` must be null (key is bundled).
- **PEM**: `CertificatePath` + optional `KeyFilePath` + optional `PassPhrase`.

**Confirmed decisions:** file paths on disk (not DB bytes), delete allowed even if cert is assigned to hosts (dangling FK is acceptable at runtime).

---

## Phase 1 — Core Domain

### New files in `src/ProxyManager.Core/AggregatesModel/CertificateAggregate/`

**`CertificateFormat.cs`**
```csharp
public enum CertificateFormat { Pfx, Pem }
```

**`Certificate.cs`** — aggregate root extending `Entity`
- Private constructor; internal `Reconstitute(id, name, format, certPath, keyFilePath, passPhrase, createdAt, updatedAt)`
- Public `Create(name, format, certPath, keyFilePath?, passPhrase?)` factory:
  - Guards: non-empty `name`, non-empty `certPath`
  - If `format == Pfx && keyFilePath is not null` → throw `CertificateValidationException`
  - Sets `CreatedAt = UpdatedAt = DateTimeOffset.UtcNow`
- Public `Update(name?, certPath?, keyFilePath?, passPhrase?)` — applies non-null args, refreshes `UpdatedAt`
- Format is immutable (delete + re-create to change format)
- Properties: `Name`, `Format`, `CertificatePath`, `KeyFilePath?`, `PassPhrase?`, `CreatedAt`, `UpdatedAt`

**`ICertificateRepository.cs`**
```csharp
Task<Certificate?> FindAsync(Guid id, CancellationToken ct = default);
Task<IReadOnlyList<Certificate>> GetAllAsync(CancellationToken ct = default);
Task AddAsync(Certificate certificate, CancellationToken ct = default);
Task UpdateAsync(Certificate certificate, CancellationToken ct = default);
Task RemoveAsync(Guid id, CancellationToken ct = default);
```

### Modify `ProxyHostAggregate/ProxyHost.cs`
- Replace `ProxyCertificate? Certificate { get; private set; }` → `Guid? CertificateId { get; private set; }`
- Update constructor, `Reconstitute`, and `Create` to use `Guid? certificateId`
- Rename `SetCertificate(ProxyCertificate?)` → `AssignCertificate(Guid? certificateId)`

### Delete `ProxyHostAggregate/ProxyCertificate.cs`
Remove after all references are updated (compile-driven).

---

## Phase 2 — Core Messages & DTOs

### New commands in `src/ProxyManager.Core/Messages/Commands/`
- **`CreateCertificateCommand`**: `Name`, `Format` (string, parsed to enum in handler), `CertificatePath`, `KeyFilePath?`, `PassPhrase?`, `ActorId`
- **`UpdateCertificateCommand`**: `Id`, `Name?`, `CertificatePath?`, `KeyFilePath?`, `PassPhrase?`, `ActorId`
- **`DeleteCertificateCommand`**: `Id`, `ActorId`
- **`AssignCertificateCommand`**: `ProxyHostId`, `CertificateId?` (null = unassign), `ActorId`

### New queries in `src/ProxyManager.Core/Messages/Queries/`
- **`GetCertificatesQuery`**: `Page = 1`, `PageSize = 20`
- **`GetCertificateByIdQuery`**: `Id`

### New events in `src/ProxyManager.Core/Messages/Events/`
- **`CertificateCreatedEvent`**: `Id`, `Name`, `Format` (string), `OccurredAt`
- **`CertificateUpdatedEvent`**: `Id`, `Name`, `OccurredAt`
- **`CertificateDeletedEvent`**: `Id`, `OccurredAt`

### New DTO `src/ProxyManager.Core/DTOs/CertificateDto.cs`
`Id`, `Name`, `Format` (string), `CertificatePath`, `KeyFilePath?`, `CreatedAt`, `UpdatedAt`
**`PassPhrase` intentionally excluded** (security hygiene, mirrors old `ProxyCertificateDto`).

### Modify `ProxyHostDto.cs`
Replace `ProxyCertificateDto? Certificate` → `Guid? CertificateId`. Delete `ProxyCertificateDto` record.

### Modify existing commands
- **`CreateProxyHostCommand`**: Remove `CertificatePath`, `CertificateKeyPath`
- **`UpdateProxyHostCommand`**: Remove `CertificatePath`, `CertificateKeyPath`

### New exceptions in `src/ProxyManager.Core/Exceptions/`
- **`CertificateNotFoundException`**: `(Guid id)` → `"No certificate with id '{id}' was found."`
- **`CertificateValidationException`**: `(string message)` pass-through

---

## Phase 3 — Infrastructure

### New `src/ProxyManager.Infrastructure/Data/CertificateRecord.cs`
Internal sealed class: `Id`, `Name`, `Format` (int), `CertificatePath`, `KeyFilePath?`, `PassPhrase?`, `CreatedAt`, `UpdatedAt`

### Modify `ProxyHostRecord.cs`
Remove `CertificatePath`, `CertificateKeyPath`, `CertificatePassword`. Add `Guid? CertificateId`.

### New `Data/Configurations/CertificateConfiguration.cs`
- Table: `certificates`
- `name varchar(256) NOT NULL`, `format int NOT NULL`, `certificate_path text NOT NULL`, `key_file_path text NULL`, `pass_phrase text NULL`, `created_at timestamptz NOT NULL`, `updated_at timestamptz NOT NULL`
- No FK constraint to `proxy_hosts` (cross-aggregate; referential integrity enforced at app layer)

### Modify `ProxyHostConfiguration.cs`
Remove `certificate_path`, `certificate_key_path`, `certificate_password` column configs. Add `certificate_id uuid NULL`.

### Modify `ProxyManagerDbContext.cs`
Add `internal DbSet<CertificateRecord> Certificates => Set<CertificateRecord>();`
(`ApplyConfigurationsFromAssembly` auto-discovers `CertificateConfiguration`.)

### New `Repositories/PostgresCertificateRepository.cs`
Mirrors `PostgresProxyHostRepository`. Private `ToDomain(CertificateRecord)` and `ToRecord(Certificate)` helpers. `UpdateAsync` fetches tracked record and assigns each field.

### New `Repositories/InMemoryCertificateRepository.cs`
Mirrors `InMemoryProxyHostRepository` using `ConcurrentDictionary<Guid, Certificate>`.

### Modify `PostgresProxyHostRepository.cs`
- `ToDomain`: replace cert-field mapping with `r.CertificateId`
- `ToRecord`: replace cert fields with `CertificateId = h.CertificateId`
- `UpdateAsync`: update `existing.CertificateId = host.CertificateId`

### Modify `InfrastructureServiceExtensions.cs`
Add `services.AddScoped<ICertificateRepository, PostgresCertificateRepository>();`

### Migration
```bash
dotnet ef migrations add AddCertificateAggregate \
  --project src/ProxyManager.Infrastructure \
  --startup-project src/ProxyManager.API
```
Migration `Up` must:
1. Create `certificates` table
2. Add `certificate_id uuid NULL` to `proxy_hosts`
3. Drop `certificate_path`, `certificate_key_path`, `certificate_password` from `proxy_hosts`

> No data migration needed (dev environment; no live cert rows).

---

## Phase 4 — API Handlers

All handlers follow the existing Wolverine convention: `Handle(Command/Query, CancellationToken)`.

### Modify existing handlers
- **`GetProxyHostsHandler.MapToDto`**: replace cert object with `CertificateId = host.CertificateId`
- **`CreateProxyHostHandler`**: remove `ProxyCertificate` construction; pass no cert arg to `ProxyHost.Create`
- **`UpdateProxyHostHandler`**: remove `SetCertificate` block

### New handlers in `src/ProxyManager.API/Handlers/`

**`GetCertificatesHandler`**: returns `PagedResult<CertificateDto>`, sorted by `Name`; exposes `internal static CertificateDto MapToDto(Certificate)` for reuse.

**`GetCertificateByIdHandler`**: returns `CertificateDto?`; endpoint maps null → 404.

**`CreateCertificateHandler`**: parses `Format` string → `CertificateFormat` enum (throw `CertificateValidationException` if invalid), calls `Certificate.Create`, returns `(CertificateDto, CertificateCreatedEvent)`.

**`UpdateCertificateHandler`**: finds cert or throws `CertificateNotFoundException`, calls `cert.Update(...)`, returns `(CertificateDto, CertificateUpdatedEvent)`.

**`DeleteCertificateHandler`**: finds cert or throws `CertificateNotFoundException`, removes it from the repository, deletes associated files from disk (see Addendum), returns `CertificateDeletedEvent`. No referential check (orphaned `CertificateId` on hosts is allowed).

**`AssignCertificateHandler`**: injects both `IProxyHostRepository` + `ICertificateRepository`. Finds host (throws `ProxyHostNotFoundException`). If `CertificateId` is non-null, validates cert exists (throws `CertificateNotFoundException`). Calls `host.AssignCertificate(command.CertificateId)`, returns `(ProxyHostDto, ProxyHostUpdatedEvent)`.

---

## Phase 5 — API Endpoints

### Modify `Endpoints/ProxyHostEndpoints.cs`
- Remove `CertificatePath`, `CertificateKeyPath` from `CreateProxyHostRequest` and `UpdateProxyHostRequest`
- Add assign endpoint to existing `group`:

```
PUT /proxyhosts/{id:guid}/certificate
Body: { "certificateId": "guid-or-null" }
Returns: Ok<ProxyHostDto> | 404 (host not found) | 400 (cert not found)
```
`CertificateNotFoundException` → 400 (the host was found; bad cert reference is a client validation error).

### New `Endpoints/CertificateEndpoints.cs`
Extension method `MapCertificateEndpoints(this WebApplication app)`, group prefix `/certificates`, `RequireAuthorization()`.

| Method | Route | Handler | Success | Errors |
|--------|-------|---------|---------|--------|
| GET | `/certificates` | `GetCertificatesQuery` | 200 `PagedResult<CertificateDto>` | — |
| GET | `/certificates/{id:guid}` | `GetCertificateByIdQuery` | 200 `CertificateDto` | 404 |
| POST | `/certificates` | `CreateCertificateCommand` | 201 Created | 400 validation |
| PUT | `/certificates/{id:guid}` | `UpdateCertificateCommand` | 200 `CertificateDto` | 404 |
| DELETE | `/certificates/{id:guid}` | `DeleteCertificateCommand` | 204 NoContent | 404 |

Request records defined at top of file (matching `ProxyHostEndpoints` pattern):
- `CreateCertificateRequest`: `Name?`, `Format?`, `CertificatePath?`, `KeyFilePath?`, `PassPhrase?`
- `UpdateCertificateRequest`: `Name?`, `CertificatePath?`, `KeyFilePath?`, `PassPhrase?`

### Modify `Program.cs`
```csharp
app.MapCertificateEndpoints();
```
Add certificate events to Wolverine RabbitMQ block on a new durable fanout exchange `"certificates"`:
```csharp
opts.PublishMessage<CertificateCreatedEvent>().ToRabbitExchange("certificates");
opts.PublishMessage<CertificateUpdatedEvent>().ToRabbitExchange("certificates");
opts.PublishMessage<CertificateDeletedEvent>().ToRabbitExchange("certificates");
```

---

## Phase 6 — Tests

Both `tests/ProxyManager.API.Tests` and `tests/ProxyManager.Core.Tests` exist.

### Update existing tests
- `CreateProxyHostHandlerTests` / `UpdateProxyHostHandlerTests`: remove cert params from commands
- `GetProxyHostsHandler` / `GetProxyHostByIdHandler` tests: assert `CertificateId` (Guid?) not cert object

### New unit tests
- **`CreateCertificateHandlerTests`**: valid PFX, valid PEM, PFX with KeyFilePath → validation error, bad format string → validation error, empty name → exception
- **`UpdateCertificateHandlerTests`**: partial update, unknown id → not found
- **`DeleteCertificateHandlerTests`**: existing id → event returned, unknown id → not found
- **`AssignCertificateHandlerTests`**: assign valid cert, unassign (null), unknown host, unknown cert id

### New integration tests
- **`CertificateEndpointsTests`**: all 5 CRUD endpoints + assign endpoint (mirrors `ProxyHostEndpointsTests`)
- **`PostgresCertificateRepositoryTests`**: Add/Find/GetAll/Update/Remove against real Postgres (mirrors existing repo tests)

---

---

## Addendum — Filesystem Cleanup on Delete

When a certificate is deleted, `DeleteCertificateHandler` must also remove the associated files from disk after the repository `RemoveAsync` succeeds.

**File deletion rules:**
- Always attempt to delete `CertificatePath`
- If `KeyFilePath` is non-null, also delete it
- Use `File.Exists` before deleting (tolerate missing files gracefully — cert may have been manually removed)
- Log a warning if a file cannot be deleted (e.g., permission error) but do not fail the operation; the DB record is already gone
- Do **not** delete the file if `RemoveAsync` throws (keep files consistent with DB state)

**Implementation in `DeleteCertificateHandler`:**

```csharp
await repository.RemoveAsync(cert.Id, ct);

TryDeleteFile(cert.CertificatePath, logger);
if (cert.KeyFilePath is not null)
    TryDeleteFile(cert.KeyFilePath, logger);

static void TryDeleteFile(string path, ILogger logger)
{
    try { if (File.Exists(path)) File.Delete(path); }
    catch (Exception ex) { logger.LogWarning(ex, "Failed to delete certificate file {Path}", path); }
}
```

`DeleteCertificateHandler` gains a constructor dependency on `ILogger<DeleteCertificateHandler>` (Wolverine resolves this automatically from DI).

**PassPhrase** is stored in the DB only — no file to clean up.

**Integration test note:** `PostgresCertificateRepositoryTests` and `CertificateEndpointsTests` should use temp paths (e.g., `Path.GetTempFileName()`) for `CertificatePath`/`KeyFilePath` and assert the files are absent after delete.

---

## Build Order & Review Workflow

Each phase is delivered on its own git branch for review. The branch is created **before** any code changes for that phase begin. Once you're happy with the phase, commit the code and give the go-ahead to start the next phase.

| Phase | Branch name | Scope |
|-------|-------------|-------|
| 1 | `feature/cert-core-domain` | New aggregate + messages/DTOs, modify `ProxyHost`, delete `ProxyCertificate` |
| 2 | `feature/cert-infrastructure` | New record/config/repo, modify existing repo/context, generate migration |
| 3 | `feature/cert-api-handlers` | Update existing handlers, add new certificate handlers |
| 4 | `feature/cert-api-endpoints` | Update `ProxyHostEndpoints`, add `CertificateEndpoints`, update `Program.cs` |
| 5 | `feature/cert-tests` | Update broken tests first, then add new ones |

> Each branch should be created from the tip of the previous phase's committed work (not from `main`) so they stack cleanly.

## Verification

```bash
dotnet build ProxyManager.sln
dotnet test tests/ProxyManager.Core.Tests
dotnet test tests/ProxyManager.API.Tests
dotnet run --project src/ProxyManager.API/ProxyManager.API.csproj
# Then via Scalar UI or curl:
# POST /certificates  { "name": "my-cert", "format": "Pem", "certificatePath": "/certs/cert.pem" }
# GET  /certificates
# PUT  /proxyhosts/{id}/certificate  { "certificateId": "<cert-id>" }
# GET  /proxyhosts/{id}  → verify CertificateId is set
# DELETE /certificates/{id}  → verify allowed even when assigned
```
