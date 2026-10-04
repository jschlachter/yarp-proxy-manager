# Plan: Scope system routes to the management host (ADR 0005)

**Status:** Implemented

## Goal

On any user domain, every path reaches that host's backend, including `/api/**`, `/manage/**`,
`/login`, `/logout` and `/manage/api/health-states`. The management UI, API and account endpoints
keep working on the configured management host. Production's system routes come from a file
shipped in the image.

**Out of scope:**
- Rejecting user hosts whose domain is the management host.
- Changing user-route `Order`, or the routes' transforms and destinations.
- Production Quadlet or secrets work beyond the setting itself.

## Tasks

### 1. `Management:Hosts` setting — [x] done

- Add `src/ProxyManager/Options/ManagementOptions.cs` (`Section = "Management"`,
  `string[] Hosts`). Register it in an `IServiceCollection` extension with
  `.Validate(o => o.Hosts.Length > 0, …)` and `.ValidateOnStart()`.
- `appsettings.json`: `"Management": { "Hosts": [ "proxy-manager.west94.io" ] }`. No Development
  override: the local DNS alias resolves `proxy-manager.west94.io` to the dev proxy (port 8443).

**Verify:** the proxy fails to start when `Management:Hosts` is empty (covered in task 4).

### 2. Scope host-less YARP routes — [x] done

- Add `src/ProxyManager/Yarp/ManagementHostRouteFilter.cs`, an `IProxyConfigFilter`.
  `ConfigureRouteAsync` returns `route with { Match = route.Match with { Hosts = hosts } }` when
  `route.Match.Hosts` is null or empty, and the route unchanged otherwise. `ConfigureClusterAsync`
  returns the cluster unchanged.
- Register it in `Program.cs` with `.AddConfigFilter<ManagementHostRouteFilter>()` on the
  `AddReverseProxy()` chain.
- `proxysettings.Development.json`: remove the `Hosts` array from `apiRoute`.

**Verify:** add `tests/ProxyManager.API.Tests/Unit/Yarp/ManagementHostRouteFilterTests.cs`
(`[Trait("Category", "Unit")]`):
- A route with no `Hosts` comes back with `Management:Hosts`.
- A route with its own `Hosts` (as the translator produces) comes back unchanged.

### 3. Scope the proxy's own endpoints — [x] done

- In `Program.cs`, map `MapHealthStateEndpoints()` and `MapAccountEndpoints()` on
  `app.MapGroup("").RequireHost(managementHosts)` instead of on `app`. Keep the health endpoint
  ahead of `MapReverseProxy()` as today.
- Update the `MapHealthStateEndpoints` summary: it wins over `ui-api-route` only on the
  management host.

**Verify:** covered by task 4's integration tests.

### 4. Integration tests through the proxy host — [x] done

- `TestProxyAppFactory`: add `["Management:Hosts:0"] = "localhost"`. `TestServer` sends
  `Host: localhost`, so existing tests keep their behavior.
- Add `tests/ProxyManager.API.Tests/Integration/ManagementHostScopingTests.cs`
  (`[Trait("Category", "Integration")]`). Seed a user host `app.example.com` whose destination is
  a dead address (`http://127.0.0.1:9`), so a forward to it returns 502:
  - Unauthenticated `GET /manage/api/x` with `Host: app.example.com` returns 502 (user route), not
    the 401 that `ui-api-route` gives on `localhost`.
  - Authenticated `GET /manage/api/health-states` with `Host: app.example.com` returns 502, not
    the endpoint's 200.
  - `GET /login` with `Host: app.example.com` returns 502, not an OIDC challenge.
  - The same three requests on `localhost` still give 401, 200 and the challenge.
- A test that building the factory with `Management:Hosts` unset throws on startup.

- Both proxy-host test classes share the `TestProxyAppFactory.Collection` xunit collection:
  `Program.cs` replaces and freezes the static Serilog bootstrap logger, so two proxy hosts
  starting in parallel fail with "The logger is already frozen".

**Verify:** `dotnet test tests/ProxyManager.API.Tests/ProxyManager.API.Tests.csproj` passes,
including `HealthStateEndpointsTests`.

*Result:* `ManagementHostScopingTests` and `HealthStateEndpointsTests` pass (12/12, three runs).
The project's other integration classes (`CertificateEndpointsTests`, `ProxyHostEndpointsTests`,
`FileAssetClientTests`, `Postgres*RepositoryTests`) fail the same way on the base commit
(36 failures there, 30 here); they don't touch the proxy host and are not part of this change.

### 5. Ship production system routes — [x] done

- `git mv src/ProxyManager/proxysettings.json src/ProxyManager/proxysettings.Production.json`.
- `ProxyManager.csproj`: keep `proxysettings*.json` out of the build output, but set
  `CopyToPublishDirectory=PreserveNewest` for `proxysettings.Production.json` only.
- Update CLAUDE.md (Configuration and Deployment): `Management:Hosts` (env var
  `Management__Hosts__0`), and that production system routes ship in the image as
  `proxysettings.Production.json`.

**Verify:**
- `dotnet publish src/ProxyManager/ProxyManager.csproj -c Release -o <tmp>` contains
  `proxysettings.Production.json` and no `proxysettings.Development.json`.
- `./scripts/build-images.sh proxy --tag=dev`, then confirm
  `podman run --rm --entrypoint ls west94.com/proxymanager:dev /app` lists the file.

### 6. Manual check in Development — [x] done

1. Run the proxy, API and files service against the dev database (hosts present) with
   "Launch It All", and the UI with `cd src/ProxyManager.UI && npm run local`.
2. In a browser, `https://proxy-manager.west94.io:8443/` serves the UI; signing in and loading
   the proxy-host list and health badges work (agent-browser).
3. `curl -k -H "Host: <user domain>" https://proxy-manager.west94.io:8443/api/x` and `/manage/x`
   reach that host's backend, not the management API or the UI.
4. The same with `/` reaches the backend; the dev `/**` `ui-route` no longer shadows it.
5. `curl -k https://localhost:8443/manage/` no longer serves the UI (falls through to 404).

**Verify:** `dotnet test ProxyManager.sln -- --filter-trait "Category=Unit"` passes.

*Result:* unit tests pass (220). Against the dev database (`cockpit.west94.io`,
`storage.west94.io`): `/api/x`, `/manage/x`, `/`, `/login` and `/manage/api/health-states` with a
user `Host` are all forwarded to that host's backend (proxy log "Proxying to
http://pod.lab…:9001/…"). On `proxy-manager.west94.io:8443`, `/` and `/login` challenge with OIDC,
`/manage/api/health-states` is answered by the proxy (401 unauthenticated), and `/api/proxyhosts`
with a client-credentials JWT returns 200 through `apiRoute`. `https://localhost:8443/manage/`
serves the 404 page (with status 200, as `MapFallbackToFile` always has). In agent-browser the
management host redirects to the Authentik sign-in page; signing in was not done because no
interactive Authentik user credentials were available to the implementer.

## Open questions

None.
