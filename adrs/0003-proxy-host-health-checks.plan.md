# Plan: Proxy host health checks (ADR 0003)

**Status:** Done

## Goal

When creating or editing a proxy host, an admin can:

- Turn on an **active** health check and pick its policy from a dropdown. The only option is
  `ConsecutiveFailuresHealthPolicy`. The admin can set the interval, timeout, path, query, health
  address and failure threshold.
- Turn on a **passive** health check and pick its policy from a dropdown. The only option is
  `TransportFailureRateHealthPolicy`. The admin can set the reactivation period and failure rate
  limit.
- Choose the **available destinations policy**: `HealthyAndUnknown` (the default) or
  `HealthyOrPanic`.

After the host is saved, the proxy's YARP cluster for that host carries the matching `HealthCheck`,
`Metadata` and `Destination.Health`. Read-only users see the configuration. Existing hosts don't
change.

All users can see each host's **live health state** (Healthy, Unhealthy or Unknown, with the active
and passive breakdown) on the route list and the route detail page. It refreshes about every 10 s.

**Out of scope:**
- Health history or alerts.
- Aggregating state across several proxy replicas.
- The global `TransportFailureRateHealthPolicyOptions`.
- Custom policies.
- Health checks on the system routes in `proxysettings.*.json`.

### Wire shape (request, and `ProxyHostDto.healthCheck`)

```json
"healthCheck": {
  "availableDestinationsPolicy": "HealthyAndUnknown",
  "active": {
    "policy": "ConsecutiveFailures",
    "intervalSeconds": 15, "timeoutSeconds": 10,
    "path": "/health", "query": null, "healthAddress": null,
    "consecutiveFailuresThreshold": 2
  },
  "passive": {
    "policy": "TransportFailureRate",
    "reactivationPeriodSeconds": 120,
    "failureRateLimit": 0.3
  }
}
```

Every field is nullable except `policy` and `availableDestinationsPolicy`. A null field means YARP's
default applies. A null `active` or `passive` means that check is off.

## Tasks

### 1. Domain value object — [x] done

Start in `src/ProxyManager.Core/AggregatesModel/ProxyHostAggregate/`.

- Add `HealthCheckSettings.cs`. It holds these records:
  - `HealthCheckSettings(ActiveHealthCheck? Active, PassiveHealthCheck? Passive, AvailableDestinationsPolicy AvailableDestinationsPolicy)`.
    It rejects both checks being null. The aggregate stores `null` for "no checks".
  - `ActiveHealthCheck(ActiveHealthCheckPolicy Policy, TimeSpan? Interval, TimeSpan? Timeout, string? Path, string? Query, string? HealthAddress, int? ConsecutiveFailuresThreshold)`
  - `PassiveHealthCheck(PassiveHealthCheckPolicy Policy, TimeSpan? ReactivationPeriod, double? FailureRateLimit)`
  - The enums `ActiveHealthCheckPolicy { ConsecutiveFailures }`,
    `PassiveHealthCheckPolicy { TransportFailureRate }` and
    `AvailableDestinationsPolicy { HealthyAndUnknown, HealthyOrPanic }`.
- Validate in the record constructors (use a validating constructor body, like `DestinationUri`).
  Throw `ArgumentException` or `ArgumentOutOfRangeException` for:
  - durations ≤ 0
  - threshold < 1
  - rate limit outside (0, 1)
  - a `Path` without a leading `/`
  - a `Query` without a leading `?`
  - a `HealthAddress` that isn't an absolute http(s) URI
- `ProxyHost`:
  - Add `HealthCheckSettings? HealthCheck { get; private set; }` and
    `ConfigureHealthCheck(HealthCheckSettings? settings)`.
  - Add a `healthCheck` parameter to `Create` and `Reconstitute`, with a default of null in
    `Create`.

**Verify:** add `ProxyHostAggregateTests` (Unit) or a new `HealthCheckSettingsTests` covering:
- Each validation rule rejects its bad value and accepts its boundary. For the rate limit, 0 and 1
  are rejected and 0.5 is accepted.
- Both checks null is rejected.
- `ConfigureHealthCheck(null)` clears the settings.

### 2. Persistence and migration — [x] done

- `ProxyHostRecord`: add `HealthCheckRecord? HealthCheck`. This is a plain persistence record that
  mirrors the settings, with durations stored as seconds and enums stored as strings.
- `ProxyHostConfiguration`:
  - Map it to the `health_check` column as `jsonb`, nullable.
  - Use a `System.Text.Json` value converter. Records compare by value, so the default comparer
    works.
- `PostgresProxyHostRepository`: map the settings both ways in `ToDomain`, `ToRecord` and
  `UpdateAsync`. `InMemoryProxyHostRepository` and `FakeProxyHostRepository` store the aggregate,
  so they only need compiling.
- Run
  `dotnet ef migrations add AddProxyHostHealthCheck --project src/ProxyManager.Infrastructure`.

**Verify:** add a round-trip test to `PostgresProxyHostRepositoryTests` (Integration, needs Podman)
for:
- A host with both checks.
- Updating it to passive only.
- Clearing the checks to null.

Also check that the generated migration only adds one nullable column.

### 3. DTOs, commands, handlers, endpoint — [x] done

- `Core/DTOs/`:
  - Add `HealthCheckDto`, `ActiveHealthCheckDto` and `PassiveHealthCheckDto` in the wire shape
    above, with strings for policies and ints for seconds.
  - Add `HealthCheckDto? HealthCheck` to `ProxyHostDto`.
- Add `HealthCheckDto? HealthCheck = null` to `CreateProxyHostCommand` and `UpdateProxyHostCommand`,
  and to `CreateProxyHostRequest` and `UpdateProxyHostRequest` in `ProxyHostEndpoints.cs`. Pass it
  through.
- Add one mapper, `API/Handlers/HealthCheckMapper.cs`, with `ToDomain(HealthCheckDto?)` and
  `ToDto(HealthCheckSettings?)`:
  - `ToDomain` parses enum strings (case-insensitive) and returns null when both checks are null.
  - `ToDomain` catches `ArgumentException` and rethrows it as `ProxyHostValidationException`, with
    messages that name the field.
- `CreateProxyHostHandler`: pass the mapped settings to `ProxyHost.Create`.
- `UpdateProxyHostHandler`: if `command.HealthCheck is not null`, call `ConfigureHealthCheck`.
- `GetProxyHostsHandler.MapToDto`: fill in `HealthCheck`. The audit log serializes the DTO, so it
  picks up the change automatically.

**Verify:**
- `CreateProxyHostHandlerTests` and `UpdateProxyHostHandlerTests` (Unit) cover:
  - Creating with both checks.
  - An update that leaves out `healthCheck`, which leaves the settings unchanged.
  - An update with both checks null, which clears them.
  - An invalid rate limit, which throws `ProxyHostValidationException`.
  - An unknown policy name, which throws `ProxyHostValidationException`.
- `ProxyHostEndpointsTests` (Integration) cover:
  - A POST with `healthCheck` that returns it in the 201 body.
  - A POST with `failureRateLimit: 1.5` that returns 400.

### 4. YARP translation — [x] done

In `src/ProxyManager/Yarp/ProxyHostYarpTranslator.cs`, when `host.HealthCheck` is not null:

- Set `ClusterConfig.HealthCheck = new HealthCheckConfig`:
  - `AvailableDestinationsPolicy` is the enum name.
  - `Active` is `new ActiveHealthCheckConfig { Enabled = true, Interval, Timeout, Policy, Path, Query }`,
    with null fields left null so that YARP's defaults apply.
  - `Passive` is `new PassiveHealthCheckConfig { Enabled = true, Policy, ReactivationPeriod }`.
- Set `Metadata`:
  - `ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName` holds the threshold, when set.
  - `TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName` holds the rate limit,
    when set, formatted with `CultureInfo.InvariantCulture`.
- Set `DestinationConfig.Health` to `HealthAddress` on the `primary` destination.

**Verify:** `ProxyHostYarpTranslatorTests` (Unit) cover:
- No settings produces `HealthCheck == null` and no metadata. This matches the current output.
- Active only maps every field and the threshold metadata.
- Passive only maps the rate limit metadata, including an invariant-culture check under `de-DE`.
- The health address lands on the destination.

Also add one test that runs the translated clusters through YARP's `IConfigValidator`, obtained
from a `ServiceCollection().AddReverseProxy()` provider. It must report no errors for a fully
populated host. This catches invalid policy names before they reach production.

### 5. UI types, client and helpers — [x] done

Work in `src/ProxyManager.UI`.

- `types/`: add `HealthCheck`, `ActiveHealthCheck` and `PassiveHealthCheck` in the wire shape, and
  add `healthCheck?: HealthCheck | null` to `ProxyHost`.
- `lib/proxy-manager-client.ts`: add `healthCheck?: HealthCheck | null` to `CreateRouteRequest` and
  `UpdateRouteRequest`. The BFF routes forward the body unchanged, so they need no change.
- Add `lib/health-checks.ts` with:
  - `ACTIVE_HEALTH_POLICIES = [{ value: "ConsecutiveFailures", label: "ConsecutiveFailuresHealthPolicy" }]`
    and `PASSIVE_HEALTH_POLICIES = [{ value: "TransportFailureRate", label: "TransportFailureRateHealthPolicy" }]`
    as the dropdown sources.
  - Pure helpers `toFormState(healthCheck)` and `toPayload(formState)`. These convert between the
    form's string inputs and the payload. Blank inputs become `null`, and both checks off becomes
    `{ active: null, passive: null, … }`.
  - `validateHealthCheck(formState)`, mirroring the server rules so the form shows errors inline.

**Verify:** add `tests/unit/lib/health-checks.test.ts` covering:
- A round trip of a full config.
- Blank fields become null.
- Both checks off gives the clearing payload.
- Each validation rule.

### 6. UI form section — [x] done

- Add `components/routes/HealthCheckFields.tsx` as a controlled component (`value` and `onChange`
  on the form state) with three parts:
  - **Active health check:** an "Enabled" checkbox. When it's checked, show:
    - A policy `<select>` fed from `ACTIVE_HEALTH_POLICIES`, styled like `Input`.
    - Interval (s) and Timeout (s).
    - Path and Query.
    - Health address, with help text "Probe this address instead of the destination".
    - Failure threshold.
    - Placeholders that show YARP's defaults (15, 10, 2).
  - **Passive health check:** an "Enabled" checkbox. When it's checked, show:
    - A policy `<select>` fed from `PASSIVE_HEALTH_POLICIES`.
    - Reactivation period (s).
    - Failure rate limit (0–1), with a placeholder of 0.3.
  - **Available destinations policy:** shown only when a check is enabled. Make it a radiogroup in
    the same style as TLS Mode, with the options "Stop traffic when unhealthy" (`HealthyAndUnknown`,
    the default) and "Keep sending traffic" (`HealthyOrPanic`). Add help text: an unhealthy backend
    returns 503 under the first option.
- `RouteForm.tsx`:
  - Hold the health check form state and render `HealthCheckFields` under TLS Mode, with a
    `Separator` and a "Health checks" heading.
  - Run `validateHealthCheck` in `validate()`.
  - Include `healthCheck: toPayload(state)` in `onSubmit`, so an edit always sends it and turning
    both checks off clears them.
  - In the read-only view, add a "Health checks" block. It shows "Off", or a line for each enabled
    check with its policy and the fields that are set.
- Follow the dark, vibrant styling in the existing form (see the `feedback_ui_style` memory).

**Verify:**
- `tests/unit/components/RouteForm.test.tsx` covers:
  - Enabling active shows its fields and a policy dropdown with exactly one option.
  - Submitting sends the expected `healthCheck`.
  - Editing a host that has checks pre-fills the fields.
  - An invalid rate limit blocks submit with an inline error.
  - The read-only view renders the summary.
- Manual check with agent-browser against "Launch It All" plus the UI dev server:
  1. Create a host with active checks on a bad `Path`, using the default `HealthyAndUnknown`.
  2. After about 2 intervals, requests to the host return 503.
  3. Fix the path. Traffic resumes.
  4. Switch to `HealthyOrPanic`. Traffic flows even while the check fails.

### 7. Proxy health-state endpoint — [x] done

- Add `src/ProxyManager/Endpoints/HealthStateEndpoints.cs` next to `AccountEndpoints.cs`, with
  `MapHealthStateEndpoints()`:
  - Map `GET /manage/api/health-states` with `.RequireAuthorization("AuthenticatedUsersOnly")`.
    Call `MapHealthStateEndpoints()` in `Program.cs` before `app.MapReverseProxy()`.
  - The endpoint returns `TypedResults.Ok` with a `HealthStateDto[]`, plus `Cache-Control: no-store`.
  - Build the list from `IProxyStateLookup.GetClusters()`. Keep only clusters whose `ClusterId`
    parses as a `Guid` (the database hosts) and whose
    `Model.Config.HealthCheck?.Active?.Enabled == true || Passive?.Enabled == true`.
  - Read the `primary` destination's `Health.Active` and `Health.Passive`. Each is null when its
    check is disabled.
- Define the DTO in the proxy project:
  `HealthStateDto(Guid ProxyHostId, string Status, string? Active, string? Passive, DateTimeOffset CheckedAt)`.
  - `Status` is `Unhealthy` if any enabled check is `Unhealthy`.
  - Otherwise it is `Healthy` if any enabled check is `Healthy`. Passive state stays `Unknown` on a
    host with little traffic, so requiring every check to be `Healthy` would leave hosts amber for
    good. (This was changed in review.)
  - Otherwise it is `Unknown`.
  - Put the status rule in a small static `HealthStateMapper` so it can be unit tested.

**Verify:**
- `HealthStateMapperTests` (Unit) cover every combination: active only, passive only, both, and
  either one `Unhealthy`.
- An integration test in `ProxyManager.API.Tests` runs the `ProxyManagerApp` factory with a fake
  `IProxyHostRepository` that has one host with checks. It asserts that:
  - An authenticated `GET /manage/api/health-states` is answered by the proxy endpoint and not
    forwarded to `ui-cluster`. This locks the endpoint ordering.
  - The response lists that host with `Unknown` states.
  - An unauthenticated request gets 401.

### 8. UI live health badges — [x] done

- Add `HealthState` to `types/`.
- Add `lib/use-health-states.ts`, a client hook:
  - It fetches `/manage/api/health-states` through `apiFetch` on mount and then every 10 s, and
    clears the interval on unmount.
  - It returns a `Map<proxyHostId, HealthState>`.
  - A non-OK response or a network error leaves the map empty. This covers the UI dev server
    running without the proxy.
- Add `components/routes/HealthBadge.tsx`:
  - It shows a pill in the same style as the Enabled badge on `RouteCard`: emerald for Healthy,
    red for Unhealthy and amber for Unknown.
  - Its tooltip (the existing `Tooltip`) shows the active and passive states and the time of the
    last check.
  - It renders nothing when the host has no checks or there's no state.
- Wiring:
  - `RouteListClient` calls the hook once and passes `healthState` to each `RouteCard`, which shows
    the badge next to the Enabled badge.
  - `RouteDetailClient` shows the badge near the heading when the host has checks.

**Verify:**
- Add a `tests/unit/lib/use-health-states.test.ts` test with fake timers. It covers:
  - The endpoint is polled every 10 s.
  - The interval is cleared on unmount.
  - A 404 gives an empty map.
- Add a `HealthBadge` test covering the color and label for each status, and that nothing renders
  when there is no state.
- Extend the `RouteCard` test so the badge shows when a state is passed.
- Manual check with agent-browser, as in task 6: while the bad path is in place, the route card
  turns Unhealthy within about 2 intervals plus one poll. After the fix it turns Healthy.

### 9. Docs — [x] done

- `CLAUDE.md`, in the ProxyManager bullet:
  - Note that user routes may carry health checks via `ProxyHost.HealthCheck` (ADR 0003).
  - Note that the proxy itself serves `GET /manage/api/health-states`, which takes precedence over
    `ui-api-route`.
- Mark ADR 0003 as Accepted once the user approves it, and set this plan to Done when every task
  above is checked.

**Verify:** `dotnet test ProxyManager.sln -- --filter-trait "Category=Unit"` and `npm test` pass.
The integration suite passes with Podman running.

## Deviations and notes

- **Tests for the proxy app** needed a new `TestProxyAppFactory` (none existed). It uses `UseSetting`
  rather than `ConfigureAppConfiguration`, because `Program.cs` reads `RabbitMQ:Enabled` while
  building. It also sets a static OIDC configuration, so challenges stay offline.
- **`TestContainersConfig`** no longer sets `DockerHostOverride`. That setting is the hostname used to
  *reach* containers, and setting it to a socket URI gave Npgsql the host `unix:///var/run/docker.sock`.
  Every Postgres integration test failed under Podman until it was removed.
- **`RouteDetailClient`** now stores the route returned by a successful PUT. Before, a newly enabled
  check showed no badge until the page was reloaded.
- **Manual check (tasks 6 and 8).** The real stack needs an interactive Authentik login, so the check
  ran in two parts:
  - **Proxy behaviour:** a throwaway in-process harness (`TestProxyAppFactory`, a real Kestrel backend
    and real YARP probing). A bad path gave 503 with the state Unhealthy. The fixed path gave 200 with
    Healthy. `HealthyOrPanic` with the bad path gave 200 with Unhealthy.
  - **UI:** Playwright against `next dev`, behind a mock API and a front proxy that mimics
    `/manage/api/health-states`. It covered the create form (one-option dropdowns, inline errors, the
    default policy), the payloads, the badges (Unhealthy within one poll, the tooltip), the detail
    pre-fill, clearing checks, and the read-only summary.
- **Integration suite:** the new and changed integration classes pass when run on their own:
  `PostgresProxyHostRepositoryTests`, `ProxyHostEndpointsTests` and `HealthStateEndpointsTests`.
  A full parallel `dotnet test ProxyManager.sln` under Podman failed broadly, with ~100 s container
  timeouts across unrelated suites. The user asked to set the integration run aside for now.
- The existing `RouteCard` snapshot was stale on `main` (from before the card restyle), so it was
  refreshed.

## Open questions

None. The unhealthy-host behavior was settled with the user: the UI exposes
`AvailableDestinationsPolicy`, defaulting to `HealthyAndUnknown`.
