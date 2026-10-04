---
name: podman-quadlet
description: This repo's conventions for Podman Quadlet units in `systemd/` (per-service folders, deploy.sh, proxymanager pod and network, naming, secrets). Use together with the rootless-quadlet skill whenever the user mentions Quadlet, Podman systemd units, adding a new container/service to the pod, `systemd/*.container` files, converting a compose file or `podman run` command to systemd, container healthchecks or startup ordering under systemd, Podman secrets, or debugging a Quadlet unit that won't generate or start, even if they don't say "Quadlet" explicitly.
---

This repo's conventions for Podman Quadlet units under `systemd/`. General rootless Quadlet
knowledge (how the generator works, generated service names, host prerequisites, healthchecks
and `Notify=healthy`, secrets, user namespaces, hardening, escaping, generic debugging) lives in
the `rootless-quadlet` skill (`.claude/skills/rootless-quadlet/`). Load it alongside this one; where the two differ, this
file wins. Paths are relative to the repo root.

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
  shared `EnvironmentFile=.env`; don't put new credentials there (see Repo-specific practices).
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

## Repo-specific practices

- Credentials always go in Podman secrets created by the service's `deploy.sh`. Some app units
  still use a shared `EnvironmentFile=.env`; don't add credentials to it, and when touching a unit
  that reads one from there, suggest migrating it to a secret.
- Databases and brokers that other units depend on (PostgreSQL, RabbitMQ) should carry a
  `HealthCmd=` with `Notify=healthy`, so the API and files service start only once they're ready.
- Prefix everything with `proxymanager-` and keep file name = `ContainerName=`.

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

## Debugging checklist (repo-specific)

For general failures (unit not generated, bind-mount permissions, escaping, secrets not found),
use the checklist in `rootless-quadlet`. Specific to this repo:

- **`proxymanager-<svc>.service` won't start on a fresh host:** `systemd/proxymanager/deploy.sh`
  hasn't run yet, so `proxymanager.pod` / `proxymanager.network` don't exist.
- **Another service can't reach the new one:** it must be in `Pod=proxymanager.pod`; ports for
  host access go on `proxymanager.pod`, not on the container.
- **Generator dry run on the Podman Desktop VM:**
  `podman machine ssh podman-machine-default -- /usr/libexec/podman/quadlet -dryrun -user`.

When reviewing existing units, check against these conventions and `rootless-quadlet`, and report
deviations as suggestions instead of silently rewriting working config.
