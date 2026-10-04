# ADR 0003: Optional YARP health checks on proxy hosts

**Status:** Accepted

## Context

Users want to set up YARP destination health checks when they create or edit a proxy host. See the
[YARP docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/dests-health-checks?view=aspnetcore-10.0).
YARP has two independent kinds:

- **Active:** YARP probes a health endpoint on a schedule. Settings are `Enabled`, `Interval`
  (default 15 s), `Timeout` (default 10 s), `Policy` (required), `Path` and `Query`, plus an
  optional `Destination.Health` address to probe instead of the destination address. The only
  built-in policy is `ConsecutiveFailures` (`ConsecutiveFailuresHealthPolicy`). It reads the cluster
  metadata key `ConsecutiveFailuresHealthPolicy.Threshold` (default 2).
- **Passive:** YARP watches proxied traffic for transport failures. Settings are `Enabled`,
  `Policy` (required) and `ReactivationPeriod`. The only built-in policy is `TransportFailureRate`
  (`TransportFailureRateHealthPolicy`). It reads the cluster metadata key
  `TransportFailureRateHealthPolicy.RateLimit`, a value in (0, 1) that defaults to the global 0.3.

Each proxy host becomes exactly one YARP cluster with one destination (`primary`), built in
`ProxyManager/Yarp/ProxyHostYarpTranslator`. YARP's default `AvailableDestinationsPolicy` is
`HealthyOrPanic`. When no destination is healthy, it routes to all of them anyway. With a single
destination, a health check under that default would never change routing. `HealthyAndUnknown`
removes the unhealthy destination instead, and the request gets a 503.

The proxy uses the default `app.MapReverseProxy()`, which already includes the passive health
check middleware.

## Decision

- Add an optional `HealthCheckSettings` value object to the `ProxyHost` aggregate, with three
  parts:
  - `Active?` holds the policy, interval, timeout, path, query, health address and consecutive
    failures threshold.
  - `Passive?` holds the policy, reactivation period and failure rate limit.
  - `AvailableDestinationsPolicy` is `HealthyAndUnknown` or `HealthyOrPanic`.

  A check that is present is enabled, and a null check is disabled. The host has no settings object
  when neither check is enabled. Every tuning field except the policy is optional, and a null field
  means YARP's default applies.
- Policies are enums whose values are YARP's registered names (`ConsecutiveFailures`,
  `TransportFailureRate`), so the translator passes them straight through. The UI shows each one in
  a dropdown under its class name (`ConsecutiveFailuresHealthPolicy`,
  `TransportFailureRateHealthPolicy`). For now each list has one entry.
- The UI shows `AvailableDestinationsPolicy` as a choice whenever a check is enabled. It defaults to
  `HealthyAndUnknown`, so that an unhealthy backend actually stops receiving traffic.
- The API adds a nullable `healthCheck` object to the create and update requests and to
  `ProxyHostDto`. Durations are sent as whole seconds. On update, a missing or null `healthCheck`
  leaves the settings unchanged. An object with both `active` and `passive` null clears them.
- `ProxyHostYarpTranslator` maps the settings to `ClusterConfig.HealthCheck`, to cluster `Metadata`
  (the policy parameters) and to `DestinationConfig.Health`.
- The settings are persisted in one nullable `jsonb` column, `health_check`, on `proxy_hosts`.
- The domain validates values: durations > 0, threshold ≥ 1, rate limit in (0, 1), `Path` starting
  with `/`, `Query` starting with `?`, and the health address as an absolute http(s) URI. Invalid
  values return 400 through the existing `ProxyHostValidationException`.
- **Live health state.** YARP keeps destination health only in the proxy process's memory. The proxy
  itself serves `GET /manage/api/health-states`:
  - It reads the state from `IProxyStateLookup`.
  - It requires the existing cookie session (`AuthenticatedUsersOnly`).
  - For each database-backed host that has checks, it returns the `primary` destination's active
    state, passive state and overall state.

  A minimal-API endpoint has the default `Order` 0, so it takes precedence over the YARP
  `ui-api-route` (`Order` 1) for this one path. It also gets the existing 401-instead-of-redirect
  handling for `/manage/api`. The UI polls this endpoint about every 10 s on the route list and
  detail pages, and shows a health badge on each host that has checks.

## Alternatives

- **Passive only, fixed defaults, one "Enable health checks" toggle.** This is the simplest option.
  It wasn't chosen because the user explicitly wants both kinds and every per-cluster parameter
  exposed.
- **Keep YARP's `HealthyOrPanic` default.** This needs no extra setting, but with one destination
  the checks would have no effect. The user chose to expose the policy and default it to
  `HealthyAndUnknown`.
- **Separate columns or an owned table instead of `jsonb`.** Those columns would always be read and
  written together and never queried. One `jsonb` column keeps the migration and the record mapping
  small.
- **An explicit `Enabled` flag on each check (mirroring YARP).** This would let a disabled check
  keep its values, but it adds a second way to say "off". Nobody asked to keep settings while a
  check is disabled.
- **Health state through the management API.** The API could call an internal proxy endpoint with a
  service token, or cache snapshots that the proxy publishes over RabbitMQ. Either would keep the
  API as the UI's only data source. They weren't chosen because both need a new service-auth scheme
  or make the proxy a publisher. That is extra machinery for read-only data that only the proxy
  has.
- **Expose the global `TransportFailureRateHealthPolicyOptions`** (`DetectionWindowSize`,
  `MinimalTotalCountThreshold`, `DefaultFailureRateLimit`). These are process-wide options, not
  per-cluster ones, so they don't belong on a proxy host. They're out of scope.

## Consequences

- When a host uses `HealthyAndUnknown` and its backend fails the check, requests get 503 instead of
  being tried. That is the point of the feature, but a misconfigured health path can take a host
  offline. The UI copy has to say so.
- Adding another policy later means one enum value plus its parameter fields. There is no plugin
  model.
- One path under `/manage/api` is served by the proxy instead of the UI's BFF. This depends on
  endpoint ordering, so an integration test locks it in. Running the UI dev server without the
  proxy in front means health badges don't appear, and the UI treats the missing endpoint as "no
  data".
- Health state is per proxy process and resets on restart. States start as `Unknown`, and a passive
  state returns to `Unknown` after reactivation. Running several proxy replicas would need an
  aggregated view, and this design doesn't provide one.
- Existing hosts migrate with `health_check = NULL`, so their behavior doesn't change.
