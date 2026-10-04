# ADR 0004: Load database proxy routes at proxy startup

**Status:** Accepted

## Context

`DatabaseProxyConfigProvider` starts with an empty `InMemoryConfig`. Only `Reload()` fills it, and
two things call `Reload()` today:

- `ProxyHostChangedHandler`, when a `ProxyHostCreated`, `ProxyHostUpdated` or `ProxyHostDeleted`
  event arrives on the `proxy-manager-config-reload` queue.
- `ProxyConfigSeedService.StartAsync`, but only after it has seeded hosts into an **empty**
  database. When the database already has hosts, it returns early without reloading.

So after any restart of the proxy, whether a deploy, a crash or a host reboot, every user-managed
proxy host returns 404 or falls through to the UI route. This lasts until someone creates, edits or
deletes a host. Only the system routes from `proxysettings.*.json` keep working.

Startup already depends on the database. The seed service calls `IProxyHostRepository.GetAllAsync`
in `StartAsync`, so if the database is unreachable, host startup throws and systemd restarts the
container.

## Decision

- `ProxyConfigSeedService.StartAsync` always calls `reloader.Reload()` once, after the seed step,
  whether or not it seeded anything. Seeding and the initial load stay in the one hosted service
  that already runs at startup.
- The service's summary comment says it both seeds and performs the initial route load.

In minimal hosting, user hosted services start before Kestrel, so the routes are in place before
the proxy accepts its first request.

## Alternatives

- **Load in the provider's constructor or lazily on the first `GetConfig()`.** This would make the
  provider correct on its own. It wasn't chosen: it puts a blocking database call inside YARP's
  config pipeline, and on the constructor path a DI resolution could fail with a database error.
- **A separate `ProxyConfigLoadService`.** This is cleaner in name only. It adds a second hosted
  service that must be ordered after the seed service, for one line of behavior.
- **Periodic polling of the database.** This would also cover lost events, but it adds a timer and
  constant database load. The durable RabbitMQ queue already delivers events that arrive while the
  proxy is down, and the startup reload covers everything else.
- **Retry or start empty when the database is down.** Nothing in the current behavior asks for
  this. Failing fast and letting systemd restart the container matches how the seed service already
  behaves.

## Consequences

- After a restart, the proxy serves every enabled database host immediately.
- The database is still a hard startup dependency. The proxy won't start while PostgreSQL is
  unreachable, which is the same as today.
- Events queued while the proxy was down trigger redundant reloads on startup. Each one only
  re-reads the same data, so this is harmless.
