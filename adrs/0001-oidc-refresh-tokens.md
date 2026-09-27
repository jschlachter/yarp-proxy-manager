# 0001. Silent OIDC token refresh in the proxy cookie session

**Status:** Accepted

## Context

ProxyManager (the YARP host) signs browser users in with OpenID Connect against Authentik and stores
the tokens in its auth cookie (`SaveTokens = true`, scope includes `offline_access`). The
`BearerToken` transform then forwards `access_token` to the UI BFF (`/manage/api/**`), the management
API (`/api/**`) and the files service (`/api/files/**`). When the access token expires those calls fail
and users are bounced back to Authentik.

A refresh attempt already exists inline in `Program.cs` (`OnValidatePrincipal`) using
`West94.AspNetCore.Authentication/TokenClient`, but it does not work reliably:

- It sends a hard-coded `client_id` (`"proxy-manager"`) and no `client_secret`, so Authentik rejects
  the refresh for our confidential client and the user is signed out.
- It downloads the discovery document on every refresh instead of using the OIDC handler's cached
  configuration.
- Parallel requests with an expired token each refresh on their own. With refresh-token rotation,
  every request except the first presents a used refresh token, fails, and signs the user out.
- It indexes `.Token.expires_at` directly and parses it with the current culture, so a missing or
  differently formatted value throws. It also sets `ShouldRenew` after rejecting the principal.
- If a session can't be refreshed, `fetch` calls under `/manage/api` get a 302 to Authentik. Those
  calls can't follow the redirect, so they fail silently.

Constraint: no new third-party dependencies. We use only the BCL and ASP.NET Core shared framework
(`Microsoft.AspNetCore.Authentication.OpenIdConnect` is already referenced).

Scope: only the system routes. User-defined proxy hosts stay unauthenticated.

## Decision

Replace the inline handler with a small, testable refresh component in
`src/ProxyManager/West94.AspNetCore.Authentication/`:

1. **`OidcTokenRefresher`** (singleton) performs the `refresh_token` grant against the token endpoint
   from `OpenIdConnectOptions.ConfigurationManager` (cached discovery). It sends the configured
   `ClientId`/`ClientSecret`. Refreshes for the same refresh token are de-duplicated with an
   `IMemoryCache` entry holding a `Lazy<Task<…>>` that lives for about 30 seconds. Concurrent and
   immediately-following requests share one token-endpoint call and one result.
2. **`CookieOidcRefreshHandler`** backs `CookieAuthenticationEvents.OnValidatePrincipal`. When
   `expires_at` (parsed invariantly) is within a configurable leeway (default 60 s), it refreshes.
   On success it updates `access_token`, `expires_at`, the rotated `refresh_token` and `id_token`
   (the last two only if returned) and sets `ShouldRenew`. If the refresh fails, or the token has
   expired and there is no refresh token, it rejects the principal and signs out.
3. An `AddOidcTokenRefresh()` extension wires it up and keeps `Program.cs` clean.
4. **401 instead of redirect for API calls:** the OIDC `OnRedirectToIdentityProvider` event returns
   401 for requests under `/api` and `/manage/api`. Page navigations still redirect.
5. The UI gets one `apiFetch` helper that sends the browser to `/login?returnUrl=…` on a 401, and
   the existing `fetch("/manage/api/…")` calls use it.

`TokenClient`, `AccessTokenResponse` and the unused `West94WebAppOptions` are removed or folded
into the new component.

## Alternatives

- **Fix the existing inline handler in place:** send the right client credentials and parse
  `expires_at` safely. This is the simplest option and fixes the most common failure. It was
  rejected because it leaves the rotation race and the silent XHR failures, which were the reasons
  for this work, and keeps untestable logic in `Program.cs`.
- **Duende.AccessTokenManagement / IdentityModel:** these libraries solve the problem fully, but
  they break the no-dependency constraint.
- **Refresh earlier (bigger leeway) without a lock:** this narrows the race but does not remove it.
- **Distributed lock/cache (Redis, Postgres):** only needed with several proxy replicas, and we
  run one.
- **Don't refresh; shorten sessions and re-challenge:** this is the behavior users are already
  unhappy with.

## Consequences

- Sessions last as long as Authentik's refresh-token lifetime, instead of the access-token
  lifetime.
- The de-duplication is per process. If the proxy is ever scaled out, the rotation race comes
  back across instances, and the cache needs to move to a shared store.
- The cookie grows slightly when a rotated refresh token and id token are stored. Cookie chunking
  already handles the size.
- Claims (e.g. `groups`) are not re-read on refresh. Group changes take effect at the next full
  sign-in, as they do today.
- The UI must route `/manage/api` calls through `apiFetch` to get the redirect-on-401 behavior.
