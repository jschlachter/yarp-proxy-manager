# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build entire solution
dotnet build ProxyManager.sln

# Run the services individually (dev ports from Properties/launchSettings.json + .vscode/launch.json)
dotnet run --project src/ProxyManager/ProxyManager.csproj              # proxy      https://localhost:7129
dotnet run --project src/ProxyManager.API/ProxyManager.API.csproj     # mgmt API   https://localhost:5001
dotnet run --project src/ProxyManager.Files/ProxyManager.Files.csproj # files svc  https://localhost:5002
# UI: see src/ProxyManager.UI section below (http://localhost:3000)

# Publish release builds
dotnet publish src/ProxyManager/ProxyManager.csproj -c Release
dotnet publish src/ProxyManager.API/ProxyManager.API.csproj -c Release
dotnet publish src/ProxyManager.Files/ProxyManager.Files.csproj -c Release

# Container images (Podman) — target is proxy|api|files|ui|all
./scripts/build-images.sh all --tag=dev

# Deploy Quadlet units + certs to the Podman machine
./scripts/deploy-vm.sh

# Generate a self-signed localhost PFX for local TLS (run from scripts/ssl/)
scripts/ssl/generate-pfx-cert.sh -o localhost.pfx
```

VSCode compound launch config **"Launch It All"** runs the proxy, API, and files service together.
`proxysettings.Development.json` expects the API at `:5001`, the files service at `:5002`, and the UI at `:3000` — match those ports when running services by hand.

### Tests

```bash
# All .NET tests
dotnet test ProxyManager.sln

# One project
dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj
dotnet test tests/ProxyManager.Core.Tests/ProxyManager.Core.Tests.csproj
dotnet test tests/ProxyManager.Files.Tests/ProxyManager.Files.Tests.csproj

# Unit tests only (what CI runs) — tests are tagged [Trait("Category", "Unit"|"Integration")]
dotnet test ProxyManager.sln -- --filter-trait "Category=Unit"

# One test by name
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
```

Tests use **Microsoft.Testing.Platform** (set in `global.json`, not VSTest) with **xunit v3**.
`Category=Integration` tests start a real PostgreSQL via **Testcontainers**, so a running container engine (Podman/Docker) is required; CI skips them.
`ProxyManager.API.Tests` references the proxy app under the `ProxyManagerApp` extern alias and drives both hosts through `WebApplicationFactory<Program>` (each `Program.cs` ends with `public partial class Program { }` for this reason).

### UI (src/ProxyManager.UI)

```bash
cd src/ProxyManager.UI
npm run dev            # Next.js dev server, http://localhost:3000
npm run build
npm test               # Jest unit tests
npm run test:coverage
npm run test:e2e       # Playwright e2e tests
```

## Architecture

```
                          Internet
                             │
                      ┌──────▼───────┐
                      │ ProxyManager │  YARP reverse proxy + static UI shell
                      │              │──── OIDC (cookie) ──── Authentik (auth.west94.io)
                      └──┬────┬───┬──┘
      /api/files/** ─────┘    │   └───── /{**} ── UI (Next.js)
      /api/**  ──────────────┐│
                             ││ user-defined proxy routes (from DB)
                  ┌──────────▼▼─────────┐     ┌────────────────────┐
                  │  ProxyManager.API   │     │  Backend services  │
                  │  JWT Bearer, Wolverine│   │  (proxied hosts)   │
                  └───┬─────────────┬───┘     └────────────────────┘
     IFileAssetClient │             │ domain events (RabbitMQ fanout)
     (X-Files-Service-Token)        │  exchanges: proxy-hosts, certificates
                  ┌───▼──────────┐  │
                  │ ProxyManager │  ├── proxy-hosts ─► ProxyManager reloads YARP config
                  │   .Files     │  └── certificates ─► Files service cleans orphan assets
                  │ S3 → RustFS  │
                  └───┬──────────┘
                      │           ┌──────────────────────────────┐
   ProxyManager.API ──┼──────────►│ ProxyManager.Core            │ DDD aggregates, messages, DTOs
                      │           │ ProxyManager.Infrastructure  │ EF Core (Npgsql) + repositories
                      ▼           └──────────────────────────────┘
                 PostgreSQL  ◄──── both API (public schema) and Files (files schema)
```

### Projects (`src/`)

- **ProxyManager** — ASP.NET Core 10 app that *is* the reverse proxy (YARP). Serves the UI shell, authenticates browser users via OIDC, terminates TLS via SNI cert loading from `certs/`. Two config sources feed YARP:
  - **System routes** (`apiRoute`, `filesRoute`, `ui-route`, …) come from `proxysettings.{Environment}.json` → `ReverseProxy` section, loaded via `LoadFromConfig`.
  - **User-managed routes** come from the database via `Yarp/DatabaseProxyConfigProvider` (a second `IProxyConfigProvider` registered as a singleton). It reads `IProxyHostRepository`, translates through `ProxyHostYarpTranslator`, and hot-swaps an `InMemoryConfig`. Reloads are triggered by `Handlers/ProxyHostChangedHandler`, a Wolverine handler listening on the `proxy-manager-config-reload` queue bound to the `proxy-hosts` fanout exchange.
- **ProxyManager.API** — ASP.NET Core 10 management REST API. JWT Bearer against the same Authentik authority (audience-validated). Endpoint groups: **`/proxyhosts`** and **`/certificates`** (not `/routes`). Endpoints are thin: they build a `Command`/`Query` record from `ProxyManager.Core.Messages` and dispatch it with Wolverine `IMessageBus.InvokeAsync`; the work lives in `Handlers/*Handler.cs`. Domain events are published to the `proxy-hosts` and `certificates` RabbitMQ fanout exchanges. Talks to the files service through `Infrastructure/Files/FileAssetClient` (typed `HttpClient`, `X-Files-Service-Token` header). Hosted services: `DatabaseMigrationService`, `CertificateAssetReconciliationService`.
- **ProxyManager.Core** — domain library (no longer scaffolding). `AggregatesModel/{ProxyHostAggregate,CertificateAggregate,AuditLogAggregate}`, `SeedWork/` (`Entity`, `IDomainEvent`), `Messages/{Commands,Events,Queries}`, `DTOs/`, `Exceptions/` (mapped to Problem responses in endpoints), `Certificates/X509CertificateInspector`.
- **ProxyManager.Infrastructure** — data access. `ProxyManagerDbContext` (EF Core + Npgsql), `Data/Configurations/`, `Data/Migrations/`. Repositories come in `Postgres*Repository` and `InMemory*Repository` pairs; `AddProxyManagerInfrastructure()` wires the Postgres ones. Connection strings support `{{Token}}` placeholders interpolated from configuration keys at startup.
- **ProxyManager.Files** — standalone deployable microservice for asset upload/storage. Stores bytes in an S3-compatible object store (`IObjectStore` / `S3ObjectStore`) backed by **RustFS**, which is never browser-reachable — uploads proxy through this service at `/api/files/**`. Own `FilesDbContext` (PostgreSQL, `files` schema, `__ef_migrations_history`). **Dual auth**: browser calls use JWT Bearer; service-to-service calls from the API use `Auth/ServiceTokenAuthenticationHandler` (`X-Files-Service-Token`). The default authz policy accepts either scheme. Subscribes to the `certificates` fanout exchange (`files-certificate-cleanup` queue) to delete orphaned assets. Hosted services: `FilesDatabaseMigrationService`, `BucketBootstrapHostedService`, `StagedAssetSweeper`.
- **ProxyManager.UI** — Next.js 15 (App Router) frontend. See `src/ProxyManager.UI/CLAUDE.md` / `AGENTS.md`. Route group `app/(dashboard)/` for the shell; `app/api/**` BFF route handlers proxy to the management API via `lib/proxy-manager-client.ts`. `lib/auth.ts` derives RBAC from the `X-Auth-Groups` header the proxy injects; `lib/modules.ts` is the nav module registry. In production it is built and served behind the proxy.

### Messaging

Wolverine + RabbitMQ for inter-service communication. Fanout exchanges `proxy-hosts` and `certificates` are auto-provisioned. `RabbitMQ:Enabled` (default `true`) disables all transport wiring when false — useful for tests and single-service local runs. The API is the only publisher; the proxy and files service are consumers.

## Configuration

**YARP routes/clusters:** system routes live in `proxysettings.{Environment}.json` (separate from `appsettings.json`); user routes live in the database (see ProxyManager above). Custom YARP transforms: `BearerTokenTransformFactory` (`"BearerToken": "access_token"` — swaps the OIDC cookie's access token onto the `Authorization` header) and `ClaimHeaderTransformFactory` (`"ClaimHeader"` — projects claims to `X-Auth-Sub` / `X-Auth-Groups` / `X-Auth-Name` headers).

**Authentication split:**
- ProxyManager — OpenID Connect, cookie sessions, authority Authentik at `https://auth.west94.io`. Token refresh is handled in the cookie `OnValidatePrincipal` event using helpers in `src/ProxyManager/West94.AspNetCore.Authentication/`.
- ProxyManager.API and ProxyManager.Files — JWT Bearer, same authority, audience-validated. Files also accepts the service token scheme.

Auth config keys: `Authentication:Authority`, `Authentication:ClientId`, `Authentication:ClientSecret`, `Authentication:Audience` (env vars or appsettings).

**Database:** `Database:ConnectionString` (Options pattern, `{{Token}}` interpolation). Migrations are applied automatically at startup by the `*DatabaseMigrationService` hosted services — create migrations with `dotnet ef migrations add <Name> --project src/ProxyManager.Infrastructure` (or `src/ProxyManager.Files`); each has an `IDesignTimeDbContextFactory`.

**Object storage (Files):** `ObjectStorage` section — RustFS S3 endpoint, access/secret key, `ForcePathStyle`, region. `Upload` section bounds accepted content.

**TLS:** ProxyManager loads per-domain certs from `certs/` at startup via SNI callbacks; production certs are mounted as a volume.

## Deployment

**Podman Quadlet** (systemd-managed containers). `systemd/` holds unit files for the pod/network/volumes plus one `.container` per service: `proxymanager`, `proxymanager-api`, `proxymanager-files`, `proxymanager-ui`, `proxymanager-postgresql`, `proxymanager-rabbitmq`, `proxymanager-rustfs`. Volumes mount `~/proxymanager/config/` (proxysettings) and `~/proxymanager/certs/` (TLS). Environment comes from a `.env` file in `systemd/`.

## Logging

Both .NET hosts use Serilog: console + rolling daily file logs under `logs/` (10MB cap, 30-file retention). Paths: `logs/proxyManager-.log`, `logs/api-.log`.

## Conventions

- C# style rules for `src/**/*.cs` are in `.claude/rules/csharp-rules.md` (file-scoped namespaces, primary constructors, `IServiceCollection` extension methods to keep `Program.cs` clean, Options pattern over injected `IConfiguration`, `TypedResults` in endpoints, `[Trait("Category", …)]` on tests).
- Next.js rules are in `.claude/rules/nextjs.rules.md`; this is Next.js 15/16 App Router — verify APIs against `node_modules/next/dist/docs/` rather than assuming.
- Use the context7 MCP server for up-to-date library/framework docs.

## Key Technologies

.NET 10.0 / ASP.NET Core · YARP 2.3 · WolverineFx 5.22 (+ WolverineFx.RabbitMQ) · Serilog 4.3 · EF Core + Npgsql · AWSSDK.S3 against RustFS · Scalar.AspNetCore (OpenAPI UI, dev only) · xunit v3 on Microsoft.Testing.Platform · Testcontainers · Authentik (external OIDC) · Next.js 15 · Podman / Quadlet.
