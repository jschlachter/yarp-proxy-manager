# ADR 0005: Scope system routes to the management host

**Status:** Accepted

## Context

The proxy serves two kinds of routes from one YARP route table:

- **System routes** from `proxysettings.{Environment}.json`: `filesRoute` (`/api/files/**`),
  `apiRoute` (`/api/**`), `ui-api-route` (`/manage/api/**`) and `ui-route` (`/manage/**`, or
  `/**` in Development). Apart from the dev `apiRoute`, none sets `Hosts`, and their `Order` is 0
  to 2.
- **User routes** from the database. `ProxyHostYarpTranslator` matches each host's domains on
  `/{**catch-all}` with `Order = 100`.

YARP picks the matching route with the lowest `Order` first, so a host-less system route beats
every user route on its path. `app.example.com/api/x` goes to the management API, and
`app.example.com/manage/x` goes to the UI. In Development the `/**` `ui-route` shadows every user
route. The proxy's own minimal-API endpoints have the same problem: `/login`, `/logout`,
`/signin-oidc` and `/manage/api/health-states` answer on every domain, because a literal path
beats a user route's catch-all.

Production loading is also unsettled. `Program.cs` loads only `proxysettings.{Environment}.json`.
The csproj never publishes any `proxysettings*.json`, the image runs as `Production`, the Quadlet
mounts only `certs/`, and no `proxysettings.Production.json` exists. As built, production would
have no system routes at all. The proxy isn't deployed to production yet, so nothing depends on
the current layout.

## Decision

- A required setting, `Management:Hosts` (a list of host names), names the domain or domains the
  proxy's own UI and API live on. It is `proxy-manager.west94.io` in `appsettings.json`, with no
  Development override: a local DNS alias already points that name at the dev proxy on port 8443,
  so dev and production use the same host. Startup fails if it is empty.
- A YARP `IProxyConfigFilter` sets `Match.Hosts` to `Management:Hosts` on every route that has no
  `Hosts`. Only system routes lack them, because the translator always sets a user route's
  domains. A system route that sets its own `Hosts` keeps them.
- The proxy's minimal-API endpoints (account and health-state) are mapped in a route group with
  `RequireHost(Management:Hosts)`.
- Authentication middleware runs only for requests on `Management:Hosts`. The OIDC handler claims
  its callback paths (`/signin-oidc`, `/signout-callback-oidc`, `/signout-oidc`) inside
  `UseAuthentication()`, before routing and on any host, so a backend that uses the same paths would
  never receive them. User routes carry no authorization policy and the cookie is host-only, so
  nothing on a user domain needs authentication.
- `proxysettings.json` is renamed to `proxysettings.Production.json` and published into the image.
  `Program.cs` keeps loading only `proxysettings.{Environment}.json`. The dev file drops its
  hard-coded `apiRoute` `Hosts`.

User routes keep `Order = 100`. On a user domain no system route matches any more, so the order
between the two kinds no longer matters.

## Alternatives

- **Write `Hosts` into each system route in the JSON files.** This is the simplest option, but the
  domain is then repeated in every route of every environment file. It is easy to miss when a
  route is added, and the endpoints would still need the domain from somewhere else.
- **Give user routes a lower `Order` than system routes.** A user host registered for the
  management domain would then take over `/api` and `/manage` on it.
- **Block user hosts on the management domain.** This guards against a different mistake and
  doesn't fix the shadowing by itself.
- **Load a base `proxysettings.json` and layer the environment file over it.** The dev and base
  files differ in transform lists and destination names. ASP.NET merges arrays by index and keeps
  both destination keys, so the merge would produce wrong routes.

## Consequences

- A user domain gets every path, including `/api`, `/manage`, `/login`, `/logout` and the OIDC
  callback paths.
- The management UI and API answer only on `Management:Hosts`. Reaching them by another name, such
  as an IP address, now falls through to the 404 page. Every deployment must set the host name.
- Production gets its system routes from the image. Changing them means rebuilding the image, or
  mounting a replacement file over `/app/proxysettings.Production.json`.
- The filter's rule ("no `Hosts` means system route") relies on the translator always setting
  domains. A user route with no domains would be scoped to the management host instead of
  matching everything, which is the safer failure.
