# Plan: Load database proxy routes at proxy startup (ADR 0004)

**Status:** Done

## Goal

When the proxy starts against a database that already holds proxy hosts, every enabled host is
routed from the first request, with no create, edit or delete event needed.

**Out of scope:**
- Database retry or backoff at startup.
- Periodic polling.
- Changes to seeding behavior.

## Tasks

### 1. Always reload after the seed step — [x] done

- In `src/ProxyManager/Services/ProxyConfigSeedService.cs`:
  - Move the existing seeding body into a private `SeedIfEmptyAsync(CancellationToken)`.
  - `StartAsync` awaits it and then calls `reloader.Reload()` once.
  - Remove the `reloader.Reload()` inside the `seeded > 0` branch, and the early `return`s'
    reliance on skipping the reload.
  - Update the XML summary to say the service also performs the initial load of database routes.
- Update the comment on `DatabaseProxyConfigProvider` ("Starts with an empty config…") to name
  `ProxyConfigSeedService` as the startup caller of `Reload`.

**Verify:** add `tests/ProxyManager.API.Tests/Unit/Services/ProxyConfigSeedServiceTests.cs` with
`[Trait("Category", "Unit")]`. Use `FakeProxyHostRepository`, a real `DatabaseProxyConfigProvider`
as the reloader, and an in-memory `IConfiguration`. Cover these cases:
- The database already holds one enabled host. After `StartAsync`, `provider.GetConfig().Routes`
  contains that host's route. This is the regression test, and it fails on `main`.
- The database is empty and the config has one host route. After `StartAsync`, the host is seeded
  and routed.
- The database is empty and there are no config routes. After `StartAsync`, the config is empty and
  nothing throws.

### 2. Manual restart check — [x] done (with deviation)

1. Run the proxy against the dev database, which already contains hosts, and restart it.
2. Before touching anything in the UI, request a known user host through the proxy
   (`curl -k -H "Host: <domain>" https://localhost:7129/`). It must reach the backend and not
   return 404 or the UI.
3. Check the log for the startup load.

**Verify:** `dotnet test ProxyManager.sln -- --filter-trait "Category=Unit"` passes.

**Result:**
- `ProxyConfigSeedServiceTests.cs` already existed and used a spy reloader that asserted *no*
  reload on a non-empty database, so it encoded the bug. It was updated in place: it now uses a
  real `DatabaseProxyConfigProvider` and asserts on routes. The non-empty-database case is the
  regression test, and it fails against the old service.
- Unit suite: 218 passed.
- Manual check: the proxy ran against the dev database (2 enabled hosts) with RabbitMQ disabled, so
  no event could trigger a reload, and with `ReverseProxy__Routes__ui-route__Match__Hosts__0=localhost`
  set in the environment. That override pins the dev `ui-route` catch-all to `localhost`; it is needed
  because otherwise `ui-route` (`Order 2`) wins over the user routes (`Order 100`) for every host.
  The startup log shows both database routes added and `Loaded database proxy routes at startup.`
  before Kestrel's `Now listening`. Then, with no UI action:
  - `curl -k -H "Host: storage.west94.io" https://localhost:18443/` → `403 application/xml`, the
    same response the backend (`:9001`) gives directly. The log shows
    `Proxying to http://pod.lab.linux01.west94.io:9001/`.
  - `cockpit.west94.io` → the request is proxied to `https://pod.lab.linux01.west94.io:9090/`, then
    returns 502 because that backend's self-signed certificate fails validation. This is unrelated to
    this change.
  - An unknown host → the static 404 fallback.
- Follow-up (pre-existing, out of scope): system routes shadow user routes. `Program.cs` loads only
  `proxysettings.{Environment}.json`, and the repo has no `proxysettings.Production.json`. Even
  `proxysettings.json` has `apiRoute` (`/api/{**catch-all}`, `Order 0`, no `Hosts`) and
  `ui-api-route`/`ui-route` (`/manage/**`). Those win over user routes (`Order 100`) on every user
  host. For example, `app.example.com/api/x` is sent to the management API. In Development, the
  `ui-route` catch-all shadows user routes entirely. System routes should be pinned to the
  proxy-manager host, or user routes should be given precedence, in a separate change.

## Open questions

None.
