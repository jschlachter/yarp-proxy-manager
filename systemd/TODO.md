# Quadlet units: open issues

Findings from the reviews of `systemd/` (2026-10-11) and of `systemd/` plus `scripts/`
(2026-11-15) that are not fixed yet. Paths are relative to the repo root.

## Blocking: services won't start

- [x] **Image tags don't match, and nothing gets images onto the host.** `build-images.sh`
  defaults to `:latest` and never pushes; the units pin `:1.0.0-alpha`. Neither deploy script
  transfers images, so on a fresh host Podman tries to pull from `west94.com/...` and the unit
  fails.
  - Where: `scripts/build-images.sh` (`TAG` default), `Image=` in the proxy, API, Files and UI
    units, `scripts/deploy-remote.sh`.
  - Fix: use the same tag in the units and the build, and add a transfer step: `--push` to a real
    registry, or `podman save | ssh host podman load` / `podman image scp` in `deploy-remote.sh`.
  - Fixed 2026-11-21: `build-images.sh` defaults its tag to the one the units pin (and fails if
    they disagree); `deploy-remote.sh` copies the `west94.com/...` images with `podman save | ssh
    podman load` (skips identical IDs, checks CPU architecture, `--no-images` to skip);
    `deploy-vm.sh` checks the VM's rootless store, which is the default macOS connection.
  - Still open: images built on Apple Silicon are arm64; an amd64 host needs a
    `--platform=linux/amd64` build, which `build-images.sh` doesn't offer yet.

## Deploy scripts

- [x] **`deploy-vm.sh` copies nothing since the move to per-service folders.** It globs
  `systemd/*.container`, `*.pod`, `*.network`, `*.volume` and `.env`, but the units now live in
  `systemd/<svc>/services/`, so nothing matches; it still reloads and prints "Done!". It also
  doesn't create the Podman secrets or install `~/proxymanager/proxymanager.env`, and the
  `proxysettings.*.json` files it copies to `~/proxymanager/config` aren't mounted by any unit.
  (User-maintained script.)
  - Where: `scripts/deploy-vm.sh` (unit copy loop and config copy step).
  - Fix: run each `systemd/<svc>/deploy.sh` on the VM (as `deploy-remote.sh` does), or loop over
    `systemd/*/services/*`; drop the config copy or mount it.
  - Fixed 2026-11-21: stages each group to `~/proxymanager/deploy/` in the VM (outside the
    Quadlet directory) and runs every `deploy.sh`, proxymanager first; creates the OIDC secret
    from a local prompt or `OIDC_CLIENT_SECRET`; config copy dropped; `--restart` option.

- [ ] **`deploy-remote.sh` puts a second copy of every unit inside the Quadlet directory.** The
  staging directory defaults to the Quadlet directory, and Quadlet scans subdirectories, so each
  unit exists twice (e.g. root and `api/services/`), and a unit deleted from the root comes back
  from its staged copy.
  - Where: `scripts/deploy-remote.sh` (`STAGE_DIR` default).
  - Fix: stage outside the Quadlet directory by default, e.g. `STAGE_DIR=proxymanager/deploy`.

- [ ] **`deploy-remote.sh` can't supply the OIDC client secret.** It runs `deploy.sh` over SSH
  without a TTY, so on a host where `proxymanager-oidc-client-secret` doesn't exist yet
  `systemd/proxymanager/deploy.sh` stops with instructions.
  - Where: `scripts/deploy-remote.sh` (deploy step).
  - Fix: create the secret on the host once by hand, or have `deploy-remote.sh` allocate a TTY
    (`ssh -t`) for that step.

## Lower priority

- [ ] **Internal ports published to the host.** 5432 (Postgres), 5672/15672 (RabbitMQ) and
  9000/9001 (RustFS) are published on all interfaces, though the services reach them by container
  name. RustFS is meant to be unreachable from browsers. The `appsettings.Development.json` files
  point local dev runs at these ports on `pod.lab.linux01.west94.io`, so closing them breaks that
  workflow.
  - Where: `PublishPort=` in the postgresql, rabbitmq and rustfs units (since the pod was dropped,
    2026-11-22, each container publishes its own ports; the proxy publishes 80/8443).
  - Fix: decide whether the production stack should double as the dev backend. If not, delete
    those `PublishPort=` lines; otherwise bind them to a trusted interface
    (`PublishPort=<ip>:5432:5432`) or firewall them.

- [x] **Containerfiles set a misspelled, no-op port variable.** `ENV ASPNETCORE_HTTP_PORT=8080`
  (no trailing `S`) sits in the builder stage, so it has no effect. Ports are now set by the units
  (`ASPNETCORE_HTTP_PORTS`), so it is only misleading.
  - Where: `src/ProxyManager/Containerfile`, `src/ProxyManager.API/Containerfile`,
    `src/ProxyManager.Files/Containerfile`.
  - Fix: delete the line (and move `ASPNETCORE_ENVIRONMENT` to the runtime stage if wanted).
  - Fixed 2026-11-21: `ASPNETCORE_ENVIRONMENT` set in both stages; API and Files set
    `ASPNETCORE_HTTP_PORTS=8080` in the runtime stage; the proxy uses `Kestrel:Endpoints` instead.

- [ ] **Naming gaps that need a data/secret migration** (per the repo's `podman-quadlet` skill).
  Left alone because renaming orphans existing data or credentials on a deployed host.
  - Secret names `pm-db-pass`, `rfs-access-key`, `rfs-secret-key` lack the `proxymanager-` prefix.
  - `proxymanager-database.volume` has no `VolumeName=`, so it is named
    `systemd-proxymanager-database`; RabbitMQ's sets one.
  - RustFS data lives at `%h/proxymanager-rustfs/data`, outside `%h/proxymanager/`.
  - Fix: rename in one change together with a migration note (copy secret values, `podman volume`
    export/import or keep the old name via `VolumeName=systemd-proxymanager-database`, move the
    RustFS directory).
