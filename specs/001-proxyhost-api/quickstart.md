# Quickstart: Proxy Host Management API

**Branch**: `001-proxyhost-api` | **Date**: 2026-03-21

## Prerequisites

- .NET 10 SDK
- Docker or Podman with `docker compose` / `podman compose`
- A running RabbitMQ instance (see below)
- A valid JWT bearer token from Authentik (or a test token for local development)

---

## 1. Start RabbitMQ locally

```bash
docker run -d --name rabbitmq \
  -p 5672:5672 -p 15672:15672 \
  rabbitmq:3-management
```

Management UI: http://localhost:15672 (guest/guest)

---

## 2. Configure the API

Edit `src/ProxyManager.API/appsettings.Development.json`:

```json
{
  "Authentication": {
    "Authority": "https://auth.west94.io/application/o/proxy-manager/",
    "Audience": "proxy-manager-api"
  },
  "RabbitMQ": {
    "Host": "localhost"
  }
}
```

---

## 3. Run the API

```bash
dotnet run --project src/ProxyManager.API/ProxyManager.API.csproj
```

The API starts on HTTPS :5001 in Development mode.
Scalar API UI: https://localhost:5001/scalar/v1

---

## 4. Obtain a token (local testing)

For local integration tests, use the test JWT helper (see
`tests/ProxyManager.API.Tests/Helpers/TestJwtFactory.cs`). For manual testing against Authentik,
use the client-credentials flow:

```bash
curl -X POST https://auth.west94.io/application/o/token/ \
  -d "grant_type=client_credentials&client_id=<id>&client_secret=<secret>&scope=openid"
```

---

## 5. Verify the API

```bash
# List proxy hosts (empty initially)
curl -H "Authorization: Bearer <token>" https://localhost:5001/proxyhosts

# Create a proxy host
curl -X POST https://localhost:5001/proxyhosts \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"domainNames":["test.local"],"destinationUri":"http://localhost:3000"}'

# Get by ID (use the id from the create response)
curl -H "Authorization: Bearer <token>" https://localhost:5001/proxyhosts/<id>

# Update
curl -X PUT https://localhost:5001/proxyhosts/<id> \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"isEnabled":false}'

# Delete
curl -X DELETE -H "Authorization: Bearer <token>" https://localhost:5001/proxyhosts/<id>
```

---

## 6. Verify RabbitMQ events

After creating, updating, or deleting a proxy host, check the RabbitMQ management UI
(http://localhost:15672) under **Exchanges → proxy-hosts** to confirm messages were published.

You can also bind a temporary queue:

```bash
# In rabbitmq management UI: bind a queue to the proxy-hosts exchange, then publish a change
# and observe the message appear in the queue.
```

---

## 7. Run tests

```bash
# All tests
dotnet test ProxyManager.sln

# With coverage
dotnet test ProxyManager.sln --collect:"XPlat Code Coverage"

# Specific project
dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj
```

---

## Known Limitations (this iteration)

- **In-memory storage**: All proxy hosts and audit log entries are lost on restart.
- **Audit log retention**: SC-005 (90-day retention) is not met with in-memory storage. A future
  feature will migrate repositories to PostgreSQL.
- **No duplicate hostname check at the query layer**: The conflict check for duplicate hostnames
  is performed in the command handler against the in-memory store. Concurrent creates with the
  same hostname race; one will succeed and one will receive a 409.
