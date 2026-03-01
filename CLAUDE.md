# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build entire solution
dotnet build ProxyManager.sln

# Run individual services (from repo root)
dotnet run --project src/ProxyManager --launch-profile https
dotnet run --project src/ProxyManager.API --launch-profile https

# Publish for production
dotnet publish src/ProxyManager/ProxyManager.csproj -c Release
dotnet publish src/ProxyManager.API/ProxyManager.API.csproj -c Release
```

VS Code compound launch config "Launch Both" starts both services simultaneously.

## Architecture

Two ASP.NET Core services on .NET 10, both using Serilog for structured logging and Scalar UI for API docs (development only):

**ProxyManager** (ports 5116/7129) — The public-facing reverse proxy built on YARP. Authenticates users via OpenID Connect against Authentik. Forwards `/api/**` traffic to ProxyManager.API. Proxy routes are configured in `proxysettings.Development.json` (loaded alongside `appsettings.json`). Supports SNI-based TLS for `*.west94.io` in production using certs in `src/ProxyManager/certs/`.

**ProxyManager.API** (ports 5149/7273) — The management API for configuring proxy routes. Validates JWT Bearer tokens issued by Authentik. Exposes CRUD endpoints at `/routes` (defined in `src/ProxyManager.API/Endpoints/RouteEndpoints.cs`). All endpoints require authorization.

**ProxyManager.Core** and **ProxyManager.Infrastructure** are empty placeholder class libraries intended for future shared logic and data access.

### Request Flow

```
Client → ProxyManager (OIDC cookie auth) → YARP → ProxyManager.API (JWT auth)
```

## Key Configuration

- `appsettings.json` — Base config with Serilog (daily rolling files, 30-day retention) and Authentik OIDC/JWT settings
- `appsettings.Development.json` — Debug log level overrides, Kestrel HTTPS/SNI config, client secret
- `proxysettings.Development.json` — YARP route and cluster definitions (loaded in `ProxyManager/Program.cs` via `AddJsonFile`)

Authentik instance is managed via `docker-compose.yml` at the repo root (PostgreSQL + Authentik server/worker on ports 9000/9443).

## Technology Stack

- **YARP** (Yarp.ReverseProxy v2.3.0) — reverse proxy engine with passive health checks
- **Authentik** — self-hosted OIDC/OAuth2 provider (ClientId shared between both services)
- **Scalar** (Scalar.AspNetCore) — API documentation UI, registered only in development
- **Serilog** — structured logging to console and daily-rotating files (`logs/proxyManager-.log`, `logs/api-.log`)

## Containerfiles

Both projects have multi-stage `Containerfile`s using `mcr.microsoft.com/dotnet/sdk:10.0` for build and `aspnet:10.0` for runtime.
