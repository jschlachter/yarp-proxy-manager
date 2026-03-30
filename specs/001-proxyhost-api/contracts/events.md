# Integration Event Contracts: Proxy Host Management

**Branch**: `001-proxyhost-api` | **Date**: 2026-03-21
**Transport**: RabbitMQ via WolverineFx.RabbitMQ
**Exchange**: `proxy-hosts` (fanout, durable)
**Publisher**: `ProxyManager.API`

Integration events are published after each successful mutating command. External services
subscribe to the `proxy-hosts` exchange to receive configuration change notifications.

---

## ProxyHostCreatedEvent

Published when a new proxy host is successfully created.

**Routing key**: `proxy-host.created`

**Schema**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "domainNames": ["api.west94.io"],
  "destination": "https://backend-api:8080",
  "isEnabled": true,
  "occurredAt": "2026-03-21T14:30:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| id | string (UUID) | The new proxy host's identifier |
| domainNames | string[] | Public hostnames configured |
| destination | string | Upstream address as `scheme://host:port` |
| isEnabled | boolean | Always `true` on create |
| occurredAt | string (ISO 8601 UTC) | Timestamp of the create operation |

---

## ProxyHostUpdatedEvent

Published when an existing proxy host is successfully modified.

**Routing key**: `proxy-host.updated`

**Schema**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "domainNames": ["api.west94.io", "api-v2.west94.io"],
  "destination": "https://new-backend:9090",
  "isEnabled": false,
  "occurredAt": "2026-03-21T15:00:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| id | string (UUID) | The updated proxy host's identifier |
| domainNames | string[] | Domain names after the update |
| destination | string | Destination after the update |
| isEnabled | boolean | Enabled status after the update |
| occurredAt | string (ISO 8601 UTC) | Timestamp of the update operation |

---

## ProxyHostDeletedEvent

Published when a proxy host is successfully deleted.

**Routing key**: `proxy-host.deleted`

**Schema**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "domainNames": ["api.west94.io"],
  "occurredAt": "2026-03-21T16:00:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| id | string (UUID) | The deleted proxy host's identifier |
| domainNames | string[] | Domain names the host had before deletion |
| occurredAt | string (ISO 8601 UTC) | Timestamp of the delete operation |

---

## RabbitMQ Configuration

```
Exchange:    proxy-hosts
Type:        fanout
Durable:     true
Auto-delete: false
```

Subscribers bind their own queues to this exchange. The `ProxyManager` main proxy application
(and any other services) can consume these events to reload routing configuration dynamically.

**Wolverine configuration** (in `Program.cs`):
```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(cfg => cfg.HostName = rabbitHost)
        .AutoProvision()
        .DeclareExchange("proxy-hosts", exchange =>
        {
            exchange.ExchangeType = ExchangeType.Fanout;
            exchange.IsDurable = true;
        });

    opts.PublishMessage<ProxyHostCreatedEvent>().ToRabbitExchange("proxy-hosts");
    opts.PublishMessage<ProxyHostUpdatedEvent>().ToRabbitExchange("proxy-hosts");
    opts.PublishMessage<ProxyHostDeletedEvent>().ToRabbitExchange("proxy-hosts");
});
```
