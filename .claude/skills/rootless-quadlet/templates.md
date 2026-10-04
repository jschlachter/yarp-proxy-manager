# Rootless Quadlet templates

Starting points for the common unit types. Replace `app`, images, ports, and paths; delete
keys you don't need rather than leaving them half-configured. All files go in
`~/.config/containers/systemd/` (or a subdirectory of it) unless the project says otherwise.

## Network: `app.network`

```ini
[Unit]
Description=app network

[Network]
NetworkName=app
# Optional: fixed subnet if other config depends on it
# Subnet=10.89.10.0/24
```

## Named volume: `app-db.volume`

```ini
[Unit]
Description=app database volume

[Volume]
VolumeName=app-db
```

## Stateful dependency with readiness: `app-db.container`

```ini
[Unit]
Description=app PostgreSQL
After=network-online.target
Wants=network-online.target

[Container]
ContainerName=app-db
Image=docker.io/library/postgres:17.2
Network=app.network
Volume=app-db.volume:/var/lib/postgresql/data

Environment=POSTGRES_USER=app
Environment=POSTGRES_DB=app
# Created with: printf %s "$(openssl rand -base64 32)" | podman secret create app-db-password -
Secret=app-db-password,type=env,target=POSTGRES_PASSWORD

# Readiness: systemd marks the service started only once this passes.
HealthCmd=pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}
HealthInterval=10s
HealthStartPeriod=30s
HealthRetries=5
HealthOnFailure=kill
Notify=healthy

NoNewPrivileges=true

[Service]
Restart=always
TimeoutStartSec=900

[Install]
WantedBy=default.target
```

## Application: `app.container`

```ini
[Unit]
Description=app web service
After=network-online.target app-db.service
Wants=network-online.target
Requires=app-db.service

[Container]
ContainerName=app
Image=ghcr.io/example/app:1.4.2
AutoUpdate=registry
Network=app.network
# Loopback only; a reverse proxy on the host fronts it.
PublishPort=127.0.0.1:8080:8080

Environment=DB_HOST=app-db
Environment=DB_USER=app
Secret=app-db-password,type=env,target=DB_PASSWORD

HealthCmd=wget -qO- http://127.0.0.1:8080/healthz || exit 1
HealthInterval=30s
HealthStartPeriod=20s
HealthOnFailure=kill
Notify=healthy

NoNewPrivileges=true
DropCapability=ALL
ReadOnly=true
Tmpfs=/tmp
Memory=512m
PidsLimit=256

[Service]
Restart=always
TimeoutStartSec=900

[Install]
WantedBy=default.target
```

## Bind mount owned by the user: `files.container` fragment

```ini
[Container]
# Container app user is 1000; map the host user's UID onto it so files stay editable.
UserNS=keep-id:uid=1000,gid=1000
Volume=%h/files:/data:Z

[Service]
ExecStartPre=mkdir -p %h/files
```

## Pod: `app.pod`

Use instead of a network when containers should share `localhost`. Members set `Pod=app.pod`
and must not set `Network=` or `PublishPort=` themselves.

```ini
[Unit]
Description=app pod

[Pod]
PodName=app
Network=app.network
PublishPort=127.0.0.1:8080:8080

[Install]
WantedBy=default.target
```

The pod's service is `app-pod.service`; start that to start every member.

## Pre-pulled image: `app.image`

```ini
[Unit]
Description=app image

[Image]
Image=ghcr.io/example/app:1.4.2
```

Reference it from the container with `Image=app.image`; Quadlet orders the pull first.

## Idempotent secret setup script

```bash
#!/usr/bin/env bash
set -euo pipefail

create_secret() {
  local name=$1
  if podman secret exists "$name"; then
    echo "secret $name exists, keeping it"
    return
  fi
  local value
  value=$(openssl rand -base64 32)
  printf %s "$value" | podman secret create "$name" - >/dev/null
  echo "created secret $name (value shown once): $value"
}

create_secret app-db-password
```

## `podman run` → Quadlet key mapping

| `podman run` flag | Quadlet key |
|---|---|
| `--name` | `ContainerName=` |
| `-p` | `PublishPort=` |
| `-v vol:/x` | `Volume=vol.volume:/x` (with a `.volume` file) |
| `-v /host:/x:Z` | `Volume=/host:/x:Z` |
| `-e` / `--env-file` | `Environment=` / `EnvironmentFile=` |
| `--secret` | `Secret=` |
| `--network` | `Network=` |
| `--pod` | `Pod=` |
| `--user` | `User=` / `Group=` (in `[Container]`) |
| `--userns` | `UserNS=` |
| `--cap-drop` / `--cap-add` | `DropCapability=` / `AddCapability=` |
| `--read-only` | `ReadOnly=true` |
| `--tmpfs` | `Tmpfs=` |
| `--memory` / `--pids-limit` | `Memory=` / `PidsLimit=` |
| `--health-cmd` etc. | `HealthCmd=`, `HealthInterval=`, … |
| `--label io.containers.autoupdate=registry` | `AutoUpdate=registry` |
| `--restart` | `Restart=` in `[Service]` (not a Quadlet key) |
| `-d`, `--rm` | omit; Quadlet handles both |
| trailing command | `Exec=` |
| anything else | `PodmanArgs=` |
