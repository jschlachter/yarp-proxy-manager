# Plan: Silent OIDC token refresh (ADR 0001)

**Status:** Implemented (Task 5 Authentik browser check skipped by the user)

## Goal

Browser users stay signed in for as long as their Authentik refresh token is valid. Access tokens are
refreshed before they expire, so the UI, the management API and the files service always receive a
valid bearer token. Concurrent requests never trigger duplicate refreshes. When a session really can't
be refreshed, API calls return 401 and the UI sends the user to `/login`.

**Out of scope:** authentication or token forwarding for user-defined proxy hosts, multi-instance
(distributed) refresh coordination, and re-reading claims on refresh. No new NuGet or npm packages.

## Tasks

### 1. Token refresher with request de-duplication — [x] done

- Add `src/ProxyManager/West94.AspNetCore.Authentication/OidcTokenRefresher.cs`. It uses a typed
  `HttpClient`, `IOptionsMonitor<OpenIdConnectOptions>` and `IMemoryCache`, and exposes
  `Task<TokenRefreshResult?> RefreshAsync(string refreshToken, CancellationToken ct)`.
  - Get the token endpoint from `options.ConfigurationManager.GetConfigurationAsync(ct)`.
  - Post the form `grant_type=refresh_token`, `refresh_token`, `client_id` and `client_secret` from
    the OIDC options.
  - Return `null` on a non-success response and log the status and error code. Never log token
    values.
  - De-duplicate with `cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = 30s; return
    new Lazy<Task<…>>(…); })`. Make the key a SHA-256 of the refresh token so raw tokens aren't
    held as keys. If the result is a failure, remove the entry so a later request can retry.
- Replace `AccessTokenResponse` with a `TokenRefreshResult` record (`AccessToken`, `RefreshToken?`,
  `IdToken?`, `ExpiresIn`). Delete `TokenClient.cs` and the unused `West94WebAppOptions.cs`.
- Make the types `public` (or add `InternalsVisibleTo`) so `ProxyManager.API.Tests` can reach them
  through the `ProxyManagerApp` alias.

**Verify:** add unit tests in `tests/ProxyManager.API.Tests/Unit/Authentication/OidcTokenRefresherTests.cs`
using a fake `HttpMessageHandler` and a static `ConfigurationManager`:
- A successful response is parsed and the client credentials appear in the posted form.
- A non-2xx response returns `null`, and the next call hits the endpoint again.
- 10 parallel `RefreshAsync` calls with the same token make **exactly one** HTTP call and all get
  the same result.

### 2. Cookie `OnValidatePrincipal` handler — [x] done

- Add `CookieOidcRefreshHandler.cs` with `Task ValidateAsync(CookieValidatePrincipalContext ctx)`:
  - If there's no `access_token`, reject and sign out. This keeps today's behavior.
  - Read `expires_at` through `ctx.Properties.GetTokenValue("expires_at")` and parse it with
    `DateTimeOffset.TryParse(…, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`. If it's
    missing or unparseable, treat the token as expired.
  - If it's not within the leeway, return.
  - If it's expired or about to expire and there's no refresh token, reject and sign out.
  - Otherwise call `OidcTokenRefresher`:
    - On success, `UpdateTokenValue` `access_token` and `expires_at` (`"o"` format). Update
      `refresh_token` and `id_token` only when they're returned. Set `ShouldRenew = true`.
    - On failure, reject and sign out. Don't set `ShouldRenew`.
- Add an `OidcTokenRefreshOptions` record (`RefreshLeeway`, default 60 s) bound from the
  `Authentication:TokenRefresh` section.
- Add `AddOidcTokenRefresh(this AuthenticationBuilder, IConfiguration)` in the same folder. It
  registers `AddMemoryCache()`, `AddHttpClient<OidcTokenRefresher>()`, the handler and the options,
  and hooks `OnValidatePrincipal` through `services.AddOptions<CookieAuthenticationOptions>(Cookie
  scheme).Configure<CookieOidcRefreshHandler>(…)`.
- In `src/ProxyManager/Program.cs`, delete the inline `Events` block (lines ~92–146) and call
  `.AddOidcTokenRefresh(configuration)`.

**Verify:** add unit tests in `CookieOidcRefreshHandlerTests.cs` that build a
`CookieValidatePrincipalContext` over a `DefaultHttpContext`:
- A token that isn't expiring makes no refresher call and leaves the principal intact.
- An expiring token with a refresh token updates the tokens, keeps the old refresh token when
  none is returned, and sets `ShouldRenew`.
- A failed refresh rejects the principal and leaves `ShouldRenew` false.
- An expired token with no refresh token is rejected.
- A missing or garbage `expires_at` is treated as expired.

### 3. Return 401 instead of an OIDC redirect for API requests — [x] done

- In `AddOpenIdConnect` (`Program.cs`), set `options.Events.OnRedirectToIdentityProvider`. When
  `Request.Path` starts with `/api` or `/manage/api`, set `Response.StatusCode = 401` and call
  `ctx.HandleResponse()`. Put the predicate in a small static helper next to the refresher so it
  can be unit-tested.

**Verify:**
- Unit test: the path predicate returns true for `/manage/api/routes` and `/api/proxyhosts` and
  false for `/manage/routes` and `/`.
- Manual: with no cookie, `curl -i https://localhost:7129/manage/api/routes` returns `401` and
  `curl -i https://localhost:7129/manage/routes` returns `302` to Authentik.

### 4. UI: redirect to login on 401 — [x] done

- Add `src/ProxyManager.UI/lib/api-fetch.ts` with `apiFetch(input, init)`. It wraps `fetch` and, on
  a 401, sets `window.location.href = "/login?returnUrl=" + encodeURIComponent("/manage" + pathname
  + search)`, then returns the response.
- Replace the `fetch("/manage/api/…")` calls in `app/(dashboard)/**/*Client.tsx`: dashboard
  summary, route list/new/detail, certificate list/new/detail.

**Verify:**
- Jest test for `apiFetch`: a 401 sets `location.href` to the expected `/login?returnUrl=…`, and a
  200 passes the response through untouched.
- `npm test` and `npm run build` pass.

### 5. End-to-end check against Authentik — [x] done, except the Authentik browser steps

> **Skipped by the user (not run):** the Authentik provider change and the browser sign-in,
> expiry, parallel-refresh and revocation checks below were not performed. The unit suite
> and the CLAUDE.md update are done.

- In the Authentik `ypm` provider, temporarily set the access-token validity to 2 minutes. Confirm
  refresh-token rotation is on and `offline_access` is granted.
- Run "Launch It All" plus the UI, sign in, and leave the dashboard open past expiry. Then:
  - Reload the routes page. It loads with no Authentik redirect, and the proxy log shows one
    refresh.
  - Open the dashboard, which fires two parallel `/manage/api` calls right after expiry. The log
    shows one refresh and both calls succeed.
  - Revoke the session in Authentik and trigger a list fetch. The browser lands on `/login` and
    returns to the same page after signing in.
- Run `dotnet test ProxyManager.sln -- --filter-trait "Category=Unit"` and confirm it's green.
- Update the CLAUDE.md auth paragraph to point to `AddOidcTokenRefresh`.

## Deviations

- **Task 1:** `OidcTokenRefresher` is a singleton that takes `IHttpClientFactory` (named client
  `OidcTokenRefresher`) instead of a typed `HttpClient`. A typed client is transient, and the handler
  is captured by the cookie options, so a typed client would have pinned one `HttpMessageHandler`
  for the process lifetime.
- **Task 1:** `IMemoryCache.GetOrCreate` isn't atomic, so the lookup runs under a lock; without it
  the parallel test made two token-endpoint calls. With the lock the factory runs once per key, so
  the cache holds the `Task` itself rather than the planned `Lazy<Task<…>>` (review finding). The shared call runs with
  `CancellationToken.None` (each caller uses `WaitAsync(ct)`), so an aborted first request can't
  cancel everyone's refresh. A faulted call (network error) is evicted like a failed one and the
  exception propagates.
- **Task 3:** `IsApiRequest` lives on `OidcTokenRefreshExtensions` rather than a separate class.
  Manual check done on `https://localhost:7129` with no cookie: `/manage/api/routes` → 401,
  `/manage/routes` → 302 to Authentik.
- **Task 4:** `returnUrl` is `pathname + search`, not `"/manage" + pathname + search`. UI pages are
  served at the root (`/routes`); only BFF calls carry the `/manage` prefix, so the planned URL
  would 404 after sign-in. The navigation goes through `lib/navigation.ts` (`navigateTo`) because
  jsdom's `window.location` can't be redefined or spied on in the Jest test.
- **Task 4:** `npm run build` fails type-checking on pre-existing test-fixture drift
  (`tests/unit/api/routes*.test.ts`, `tests/unit/lib/proxy-manager-client.test.ts`: missing
  `UserSession.name`, stale `ProxyHost.name`), and `npm test` has one pre-existing failing
  `RouteCard` snapshot from the UI restyle. None involve files this plan touches; `tsc` reports no
  errors in app code. Left as-is (out of scope).

## Open questions

None. Scope (system routes only), the concurrency approach (in-memory de-duplication) and the
failure UX (401 for API calls, redirect for pages) were agreed with the user.
