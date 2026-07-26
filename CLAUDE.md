# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build entire solution
dotnet build ProxyManager.sln

# Run proxy application (HTTP :80, HTTPS :8443 in dev)
dotnet run --project src/ProxyManager/ProxyManager.csproj

# Run management API (HTTPS :5001 in dev)
dotnet run --project src/ProxyManager.API/ProxyManager.API.csproj

# Publish release build
dotnet publish src/ProxyManager/ProxyManager.csproj -c Release
dotnet publish src/ProxyManager.API/ProxyManager.API.csproj -c Release

# Build container images (Podman)
./scripts/build-images.sh

# Deploy to VM via Quadlet/systemd
./scripts/deploy-vm.sh

# Run .NET tests (all)
dotnet test ProxyManager.sln

# Run a single test project
dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj
dotnet test tests/ProxyManager.Core.Tests/ProxyManager.Core.Tests.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
```

VSCode provides compound launch config "Launch Both" to run both services simultaneously.

### UI (src/ProxyManager.UI)

```bash
cd src/ProxyManager.UI
npm run dev            # Next.js dev server, http://localhost:3000
npm run build
npm test                # Jest unit tests
npm run test:coverage
npm run test:e2e        # Playwright e2e tests
```

## Architecture

```
                        Internet
                           │
                    ┌──────▼──────┐
                    │  ProxyManager│  :80 / :8443
                    │  (YARP)      │──── OIDC ──── Authentik
                    │  + UI        │             (auth.west94.io)
                    └──┬───────┬──┘
                       │       │
          /api/**      │       │  upstream routes
          ┌────────────▼─┐   ┌─▼────────────────┐
          │ ProxyManager │   │  Backend Services │
          │    .API      │   │  (proxied hosts)  │
          │  JWT Bearer  │   └───────────────────┘
          └──────┬───────┘
                 │
        ┌────────▼────────┐
        │  ProxyManager   │
        │    .Core        │  Domain models / business logic
        └────────┬────────┘
                 │
        ┌────────▼────────┐
        │  ProxyManager   │
        │ .Infrastructure │  Data access / external services
        └────────┬────────┘
                 │
         ┌───────┴───────┐
         │               │
    ┌────▼────┐    ┌──────▼──────┐
    │PostgreSQL│    │  RabbitMQ   │  pub/sub messaging
    └─────────┘    └─────────────┘
```

Four projects in `src/`:

- **ProxyManager** – ASP.NET Core 10 app that IS the reverse proxy. Uses YARP (Yarp.ReverseProxy) to route traffic. Serves UI, authenticates users via OIDC (Authentik), manages TLS via SNI certificate loading from `certs/`.
- **ProxyManager.API** – ASP.NET Core 10 management REST API. Minimal API endpoints under `/routes` for CRUD on proxy configuration. Validates JWT Bearer tokens from the same Authentik authority.
- **ProxyManager.Core** – Class library for domain models and business logic (currently scaffolding only).
- **ProxyManager.Infrastructure** – Class library for data access and external services (currently scaffolding only).
- **ProxyManager.UI** – Next.js 15 (App Router) frontend for managing routes and certificates (see `src/ProxyManager.UI/CLAUDE.md`). Talks to ProxyManager.API; in production it is built/served behind the proxy.

The proxy routes `/api/{**catch-all}` to the API (via YARP configuration), so in production all traffic enters through ProxyManager.

Tests live in `tests/ProxyManager.API.Tests` and `tests/ProxyManager.Core.Tests` (xunit).

Publish/subscribe messaging used for inter-service communicatation and data integration

## Configuration

**YARP proxy routes/clusters** live in `proxysettings.{Environment}.json` (separate from `appsettings.json`), loaded at startup. This file drives all routing, health checks, and path transforms.

**Authentication split:**
- ProxyManager uses OpenID Connect (cookie-based, browser sessions) — authority is Authentik at `https://auth.west94.io`
- ProxyManager.API uses JWT Bearer — same Authentik authority, audience-validated

Auth config comes from environment variables or appsettings: `Authentication:Authority`, `Authentication:ClientId`, `Authentication:ClientSecret`, `Authentication:Audience`.

**TLS:** ProxyManager loads per-domain certs from `certs/` directory at startup using SNI callbacks. Production certs are mounted as a volume.

## Deployment

Deployment uses **Podman Quadlet** (systemd-managed containers). The `systemd/` directory contains unit files for:
- `proxymanager.pod` – pod networking and port mapping
- `proxymanager.container` – main proxy container
- `proxymanager-postgresql.container` – PostgreSQL for future use
- `proxymanager.network` / `proxymanager-database.volume`

Volumes mount `~/proxymanager/config/` (proxysettings) and `~/proxymanager/certs/` (TLS certs). Environment variables come from a `.env` file.

## Logging

Both apps use Serilog: console + rolling daily file logs in `logs/`. File size limit 10MB, 30-file retention. Log paths: `logs/proxyManager-.log` and `logs/api-.log`.

## Key Technologies

- .NET 10.0, ASP.NET Core
- YARP 2.3.0 (Microsoft reverse proxy library)
- Serilog 4.3.1
- Scalar.AspNetCore (OpenAPI UI, dev-only)
- Authentik (external OIDC provider)
- Podman / Quadlet for deployment
- RabbitMQ for Messaging

## Active Technologies
- C# / .NET 10.0 + ASP.NET Core Minimal APIs, WolverineFx 5.22.x, WolverineFx.RabbitMQ 5.22.x, Serilog 4.3.x, xunit 2.9.x, Microsoft.AspNetCore.Mvc.Testing 10.0.x (001-proxyhost-api)
- In-memory (ConcurrentDictionary for ProxyHosts, ConcurrentQueue for AuditLog) (001-proxyhost-api)
- TypeScript / Node.js 24 LTS + Next.js 15 (App Router), shadcn/ui, Tailwind CSS 4, Jest + RTL, Playwright (002-proxy-route-manager)
- No local database — all data (routes, maintainer assignments, audit log) is owned by the ProxyManager API (002-proxy-route-manager)
- C# 13 / .NET 10.0 + ASP.NET Core (004-db-persistence)
- PostgreSQL 16 — `proxy_hosts` and `audit_log_entries` tables; see [data-model.md](data-model.md) (004-db-persistence)

## Recent Changes
- 001-proxyhost-api: Added C# / .NET 10.0 + ASP.NET Core Minimal APIs, WolverineFx 5.22.x, WolverineFx.RabbitMQ 5.22.x, Serilog 4.3.x, xunit 2.9.x, Microsoft.AspNetCore.Mvc.Testing 10.0.x
