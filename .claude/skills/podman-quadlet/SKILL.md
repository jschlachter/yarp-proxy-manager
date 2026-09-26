---
name: podman-quadlet
description: Create, edit, review, and debug Podman Quadlet unit files (.container, .pod, .network, .volume, .build, .image, .kube) for systemd-managed containers, following this repo's conventions and Quadlet best practices. Use whenever the user mentions Quadlet, Podman systemd units, adding a new container/service to the pod, `systemd/*.container` files, converting a compose file or `podman run` command to systemd, container healthchecks or startup ordering under systemd, Podman secrets, or debugging a Quadlet unit that won't generate or start, even if they don't say "Quadlet" explicitly.
---

Quadlet turns declarative `.container`/`.pod`/`.network`/`.volume` files into systemd services
at `daemon-reload` time. This skill covers writing them well and matching this repo's layout.
Paths are relative to the repo root.

## How Quadlet works (the facts that cause most mistakes)

- A generator reads unit files from `~/.config/containers/systemd/` (rootless) or
  `/etc/containers/systemd/` (rootful) — subdirectories are scanned — and writes real `.service`
  units. You never `systemctl enable` a Quadlet unit; the `[Install]` section is honored at
  generation time. Edit the file, then `systemctl --user daemon-reload`.
- Generated service names are **not** always the file name:

  | File | Service |
  |---|---|
  | `foo.container` | `foo.service` |
  | `foo.pod` | `foo-pod.service` |
  | `foo.network` | `foo-network.service` |
  | `foo.volume` | `foo-volume.service` |
  | `foo.build` / `foo.image` | `foo-build.service` / `foo-image.service` |

- Inside Quadlet keys (`Pod=`, `Network=`, `Volume=`, `Image=`) refer to sibling units **by file
  name** (`Pod=proxymanager.pod`, `Volume=data.volume:/var/lib/x`). Quadlet then adds the
  dependency for you. In `[Unit]` (`After=`, `Requires=`) you must use the **generated service
  name** (`proxymanager-postgresql.service`).
- Only `[Container]`/`[Pod]`/etc. keys are Quadlet-specific; `[Unit]`, `[Service]`, `[Install]`
  are plain systemd and pass through.
- Containers in a pod share one network namespace: publish ports on the `.pod` (`PublishPort=`),
  never on member containers, and two members can't bind the same port.

## Conventions in this repo

Match the existing units in `systemd/` unless the user says otherwise:

- **Directory layout: one folder per service, `systemd/<SERVICENAME>/`.** Each folder contains:
  ```
  systemd/<service>/
  ├── deploy.sh        # deploys just this service (secrets, host dirs, copy units, daemon-reload)
  └── services/        # this service's Quadlet files
      ├── proxymanager-<service>.container
      └── proxymanager-<service>.volume   # only if it needs its own volume(s)
  ```
  Quadlet files are aggregated **strictly by service**, with no other grouping (no `shared/`,
  `common/`, or per-unit-type folders). Every unit file lives in exactly one service folder. Keep
  a service's units together so it can be added, changed, or removed without touching other
  folders. The per-service `deploy.sh` owns everything specific to that service, including
  creating its Podman secrets (no separate `setup.sh`), and should be idempotent: re-running it
  must not fail because a secret or directory already exists (check with
  `podman secret exists <name>` before creating, and don't regenerate an existing credential).
  Copy an existing `deploy.sh` (e.g. `systemd/rustfs/deploy.sh`) as the template so the scripts
  stay uniform (`set -euo pipefail`, `QUADLET_DIR` override).
  Units that every service depends on (`proxymanager.pod`, `proxymanager.network`) belong to the
  `proxymanager` service folder, because the proxy is the pod's anchor. Since the other services
  reference them, `systemd/proxymanager/deploy.sh` must run first on a fresh host.
- One file per service, named `proxymanager-<service>.container` (the main proxy is just
  `proxymanager.container`); `ContainerName=` set explicitly to the same name so other containers
  can address it (`http://proxymanager-api:8080`).
- Every unit is in `Pod=proxymanager.pod`, on `proxymanager.network`. Ports are published in
  `systemd/proxymanager/services/proxymanager.pod` — new externally reachable ports go there. Only
  publish what the host genuinely needs.
- Rootless, so `[Install] WantedBy=default.target`.
- `[Service]` always has `Restart=always` and `TimeoutStartSec=900` (first start may pull a large
  image).
- `[Unit]`: `After=network-online.target` plus `Wants=network-online.target`; add
  `After=`+`Requires=` on the generated service names of true dependencies (e.g. API requires
  PostgreSQL).
- Non-secret config: `Environment=Key=Value` (ASP.NET Core config uses the `Section__Key` form).
  Secrets: always `Secret=<name>,type=env,target=<ENV_VAR>` with the secret created by a
  `systemd/<service>/deploy.sh` (`systemd/rustfs/deploy.sh` and `systemd/postgresql/deploy.sh` are
  the model for the create-if-missing secret step). Some app units still use a
  shared `EnvironmentFile=.env`; don't put new credentials there (see Best practices).
- Named volumes get their own `.volume` file (`proxymanager-<thing>.volume`) referenced as
  `Volume=proxymanager-<thing>.volume:/path`. Host bind mounts use `%h` (home) and add
  `ExecStartPre=mkdir -p %h/...` so the first start doesn't fail.
- Images: app images are `west94.com/proxymanager-<svc>:<version>` built by
  `scripts/build-images.sh`; third-party images are fully qualified (`docker.io/library/...`).
- Deployment: each service's `deploy.sh` runs on the Quadlet host as the pod's user, creates that
  service's secrets, and copies `services/*` to `~/.config/containers/systemd/proxymanager/`
  (override with `QUADLET_DIR`). It copies everything in `services/`, so new unit types need no
  script change.
- `scripts/deploy-vm.sh` is a separate, standalone helper for trying Quadlets locally on the
  Podman Desktop VM. **Leave it alone** when adding or changing services; it is not part of the
  per-service deploy flow and the user maintains it on its own.
- After adding/changing units, the start command is
  `systemctl --user start proxymanager-pod.service proxymanager-ui.service`. Update the closing
  hint in `deploy-vm.sh` and the CLAUDE.md Deployment section if the set of units changes.

## Best practices

**Images**
- Fully qualify the registry; short names depend on `registries.conf` and can prompt or fail
  under systemd.
- Pin a version tag (or digest). Avoid `:latest` for anything holding state. If you want automatic
  updates, opt in explicitly with `AutoUpdate=registry` and enable `podman-auto-update.timer`
  rather than relying on a floating tag.

**Health and ordering**
- `After=`/`Requires=` only orders *process start*, not readiness. For a dependency the consumer
  needs to be actually ready (databases, brokers), give it a `HealthCmd=` and
  `Notify=healthy`; systemd then treats it as started only once healthy. Set `HealthStartPeriod=`
  to cover slow init so early failures don't count.
- The consumer should still retry its connection on startup; ordering is a mitigation, not a
  guarantee (restarts happen independently).

**Secrets and config**
- Sensitive values (passwords, API keys, client secrets, connection strings with credentials,
  tokens) go in **Podman secrets**, not in `Environment=` and not in an `EnvironmentFile=`.
  Env files are plaintext on disk, get copied around by deploy scripts, are easy to commit by
  accident, and their contents surface in `systemctl show` and `podman inspect`. A Podman secret
  is stored by Podman and only injected into the container that references it.
- Reference it with `Secret=<name>,type=env,target=<ENV_VAR>` (for apps that read env vars,
  e.g. ASP.NET `Authentication__ClientSecret`). For apps that read files, mount it instead:
  `Secret=<name>,type=mount,target=/run/secrets/<file>,mode=0400`.
- Create secrets from stdin so the value never lands in argv or shell history:
  `printf %s "$VALUE" | podman secret create <name> -`. Do this as the same user that runs the
  units (rootless secrets are per-user), from the service's `systemd/<svc>/deploy.sh`. Use `openssl rand` for generated credentials and print them once.
- Reserve `Environment=` and `EnvironmentFile=` for non-sensitive settings (URLs, feature flags,
  group names). If a shared `.env` already holds credentials, flag them and suggest migrating each
  to a secret rather than adding more.
- Never commit real credentials, in unit files, `.env`, or setup scripts.

**Escaping**
- systemd expands `%` specifiers and `$VAR` in `Exec*`/`HealthCmd`. Write `$$VAR` for a literal
  `$` evaluated inside the container (`HealthCmd=pg_isready -U $${POSTGRES_USER}`) and `%%` for a
  literal `%`.

**Storage**
- Prefer named volumes (`.volume`) to bind mounts: portable, no host path setup.
- On SELinux hosts, bind mounts need `:z` (shared label) or `:Z` (private to this container).
  For rootless bind mounts where the container user differs from yours, `:U` chowns the source
  to the container user's mapped UID. Verify what you're pointing `:U` at — it changes ownership
  of the host directory.

**Hardening (add when the image supports it)**
- `NoNewPrivileges=true`, `DropCapability=ALL` (add back only what's needed), `ReadOnly=true` with
  `Tmpfs=`/`Volume=` for the paths that must be writable, and a non-root `User=` if the image
  doesn't already drop privileges. Test each; some images break under `ReadOnly`.

**Rootless specifics**
- Binding ports below 1024 needs `net.ipv4.ip_unprivileged_port_start` lowered on the host.
- User services stop at logout unless linger is on: `loginctl enable-linger <user>`.
- `%h` is the user's home; `%t` is the runtime dir (`/run/user/UID`).

**Naming and layout**
- Prefix everything with the project (`proxymanager-`), keep file name = container name so the
  service name is predictable, and put a `Description=` on every unit — it's what shows in
  `systemctl status`.
- Comment non-obvious choices inline (why a port is published, why a timeout is long).

## Workflow

1. **Understand the source.** Convert from a compose file, `podman run`, or a description. For a
   compose file, `podlet compose` can bootstrap output if installed, but review it — it won't know
   this repo's pod/network conventions.
2. **Write the unit(s)** under `systemd/<svc>/services/`, following the conventions above. A new
   service means a `systemd/<svc>/` folder with `deploy.sh` and `services/` containing the
   `.container` (plus a `.volume` if stateful). Add port entries to `proxymanager.pod` if it must
   be reachable from the host.
3. **Validate before deploying.** Dry-run the generator to catch typos and see the resulting
   service (binary path varies: `/usr/lib/podman/quadlet` or `/usr/libexec/podman/quadlet`):
   ```bash
   podman machine ssh podman-machine-default -- \
     /usr/libexec/podman/quadlet -dryrun -user
   ```
   Unknown keys, bad section names, and missing referenced units are reported here.
4. **Deploy:** `./systemd/<svc>/deploy.sh` for one service (run `systemd/proxymanager/deploy.sh`
   first on a fresh machine, since it installs the pod and network). It copies units and runs `daemon-reload`. (`scripts/deploy-vm.sh` is the
   user's separate local Podman Desktop test helper; don't modify it.)
5. **Start and verify** on the VM:
   ```bash
   systemctl --user start proxymanager-pod.service
   systemctl --user status proxymanager-<svc>.service
   journalctl --user -u proxymanager-<svc>.service -f
   podman ps --pod
   podman healthcheck run proxymanager-<svc>
   ```

## Debugging checklist

- **Unit doesn't appear after `daemon-reload`:** generator error. Run the dryrun command, or
  `journalctl --user -b | grep -i quadlet`. Usual causes: typo in a key, wrong file extension,
  file not in a scanned directory.
- **`Unit ...service not found` when starting:** you used the file name where the generated name
  is different (`.pod` → `-pod.service`).
- **Dependency fails to start:** `systemctl --user cat <unit>` shows the generated result;
  `systemctl --user list-dependencies <unit>` shows the ordering graph.
- **Container name doesn't resolve from another container:** confirm both are on the same
  network/pod and `ContainerName=` matches what the client uses.
- **Timeout on first start:** image pull exceeded `TimeoutStartSec`; pre-pull with `podman pull`
  or raise the timeout.
- **Permission denied on a bind mount:** SELinux label (`:z`/`:Z`) or UID mapping (`:U`,
  `UserNS=keep-id`).
- **Env var not what you expected (`$` or `%` mangled):** escaping, see above.
- **Secret not found:** secrets are per-user for rootless; create it as the same user that runs
  the units (`podman secret ls`).

When reviewing existing units, check against the conventions and best practices above and report
deviations as suggestions instead of silently rewriting working config.
