# Quickstart: Database Persistence for Proxy Configuration

## Prerequisites

- .NET 10 SDK
- Docker or Podman (for local PostgreSQL)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

## 1. Start a Local PostgreSQL Instance

```bash
docker run -d \
  --name proxymanager-postgres \
  -e POSTGRES_USER=proxymanager \
  -e POSTGRES_PASSWORD=proxymanager \
  -e POSTGRES_DB=proxymanager \
  -p 5432:5432 \
  postgres:16
```

## 2. Configure Connection String

Add to `src/ProxyManager.API/appsettings.Development.json` and `src/ProxyManager/appsettings.Development.json`:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=proxymanager;Username=proxymanager;Password=proxymanager"
  },
  "Audit": {
    "RetentionDays": 90
  }
}
```

## 3. Apply Migrations

Migrations run automatically at startup. To create or update migrations manually:

```bash
# From repo root
dotnet ef migrations add <MigrationName> \
  --project src/ProxyManager.Infrastructure \
  --startup-project src/ProxyManager.API \
  --output-dir Data/Migrations
```

## 4. Run Both Services

```bash
# Terminal 1 — management API (applies migrations on startup)
dotnet run --project src/ProxyManager.API/ProxyManager.API.csproj

# Terminal 2 — proxy (loads routes from DB on startup)
dotnet run --project src/ProxyManager/ProxyManager.csproj
```

Or use the VSCode "Launch Both" compound configuration.

## 5. Verify Live Reload

```bash
# Create a ProxyHost
curl -X POST https://localhost:5001/proxyhosts \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"domainNames":["test.example.com"],"destinationUri":"http://localhost:9000"}'

# Within 2 seconds, YARP in ProxyManager should have the new route active.
# Query audit log:
curl https://localhost:5001/proxyhosts/<id>/audit \
  -H "Authorization: Bearer <token>"
```

## 6. Run Tests

```bash
# Requires Docker/Podman running (TestContainers spins up PostgreSQL automatically)
dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj
dotnet test tests/ProxyManager.Core.Tests/ProxyManager.Core.Tests.csproj
```

## Environment Variables (Production / Quadlet)

Add to `.env` file alongside the Quadlet units:

```env
Database__ConnectionString=Host=proxymanager-postgresql;Port=5432;Database=proxymanager;Username=proxymanager;Password=<secret>
Audit__RetentionDays=90
```
