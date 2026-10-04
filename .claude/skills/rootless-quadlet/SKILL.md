---
name: rootless-quadlet
description: Write, review, convert, and debug rootless Podman Quadlet units (.container, .pod, .network, .volume, .image, .build, .kube) that run as systemd user services, following current Podman best practices. Use whenever the user wants to run a container under systemd as a normal user, mentions Quadlet or `~/.config/containers/systemd`, wants to turn a `podman run` command or compose file into systemd units, asks about container healthchecks, startup ordering, auto-updates, Podman secrets, linger, or user namespaces for rootless containers, or has a Quadlet unit that won't generate or start, even if they don't say "Quadlet" or "rootless". If the repository has its own Quadlet skill or conventions, follow those for layout and naming and use this skill for everything they don't cover.
---

Quadlet is a systemd generator: at `daemon-reload` it reads declarative unit files and writes
real `.service` units that call `podman`. Rootless means those services run in the **user's**
systemd instance (`systemctl --user`) with the user's own containers, images, and secrets.

Before writing anything, check the project for existing Quadlet files and any project skill or
docs describing conventions (naming prefix, directory layout, deploy script). Project
conventions win over the defaults below; the best practices still apply.

## Mental model (the facts behind most mistakes)

- **Where files go (rootless).** The usual place is `~/.config/containers/systemd/`
  (subdirectories are scanned recursively). Admin-managed locations also exist:
  `/etc/containers/systemd/users/$UID/` (one user) and `/etc/containers/systemd/users/` (every
  user). Never put rootless units in `/etc/containers/systemd/` itself; that is rootful.
- **No `User=`.** Quadlet does not support systemd `User=`, `Group=`, or `DynamicUser=` to make a
  rootful unit run rootless. To run as user `svc`, put the file in `svc`'s search path and run
  `systemctl --user` as `svc`.
- **No `systemctl enable`.** Generated units can't be enabled; `[Install]` is applied at
  generation time. Use `WantedBy=default.target` (not `multi-user.target`, which doesn't exist
  in the user manager). Edit, then `systemctl --user daemon-reload`.
- **Generated service names:**

  | File | Service |
  |---|---|
  | `app.container` | `app.service` |
  | `app.pod` | `app-pod.service` |
  | `app.network` | `app-network.service` |
  | `app.volume` | `app-volume.service` |
  | `app.image` / `app.build` | `app-image.service` / `app-build.service` |
  | `app.kube` | `app.service` |

- **Two naming spaces.** Quadlet keys (`Pod=`, `Network=`, `Volume=`, `Image=`) reference
  sibling units **by file name** (`Network=app.network`, `Volume=app-data.volume:/data`) and
  Quadlet adds the dependency and uses the real resource name. `[Unit]` keys (`After=`,
  `Requires=`, `BindsTo=`) need the **generated service name** (`db.service`, `app-pod.service`).
- Only `[Container]`/`[Pod]`/`[Network]`/`[Volume]`/`[Image]`/`[Build]`/`[Kube]` are Quadlet
  keys. `[Unit]`, `[Service]`, `[Install]` pass through to systemd unchanged.
- Anything without a dedicated key goes in `PodmanArgs=` (or `GlobalArgs=`). Check
  `man podman-systemd.unit` for a real key first; dedicated keys are validated, `PodmanArgs` isn't.

## Host prerequisites (rootless)

Check these once per host and tell the user which ones are missing; most need root to fix.

- **Linger**, or the services stop at logout and don't start at boot:
  `loginctl enable-linger <user>` (verify: `loginctl show-user <user> -p Linger`).
- **subuid/subgid ranges** for the user (`grep <user> /etc/subuid /etc/subgid`), needed for
  multi-UID images. After changing them: `podman system migrate`.
- **A real login session for the user.** `sudo su - svc` does not set `XDG_RUNTIME_DIR` or a
  user bus, so `systemctl --user` fails. Use `machinectl shell svc@` or
  `sudo -iu svc` plus `export XDG_RUNTIME_DIR=/run/user/$(id -u)`.
- **Ports below 1024** need `net.ipv4.ip_unprivileged_port_start` lowered via sysctl (or publish
  a high port and redirect with the firewall).
- **cgroup v2 controller delegation** for resource limits (`Memory=`, `CPU*`) to take effect:
  `cat /sys/fs/cgroup/user.slice/user-$(id -u).slice/user@$(id -u).service/cgroup.controllers`.
- **Podman version** (`podman --version`). Several keys below are Podman 5.x; if the host is on
  4.x, check `man podman-systemd.unit` on that host before using a key.

## Best practices

**Images**
- Fully qualify every image (`docker.io/library/postgres:17.2`). Short names depend on
  `registries.conf` and can prompt or fail under systemd.
- Pin a tag or digest; never `:latest` for stateful services. For automatic updates, opt in with
  `AutoUpdate=registry` on a pinned-major tag and enable `podman-auto-update.timer` (user
  instance). `podman auto-update --dry-run` shows what would change; auto-update rolls back
  if the new container fails to start, which works best with `Notify=healthy`.
- Big images can exceed the start timeout on first pull: set `TimeoutStartSec=900` in
  `[Service]`, or pre-pull with a `.image` unit that the container references by `Image=app.image`.

**Networking**
- Put containers that talk to each other on a user-defined network (`.network` file,
  `Network=app.network`). The default rootless network has no container-name DNS; a custom
  network does, so containers reach each other by `ContainerName=`.
- Or put tightly coupled containers in a `.pod` (shared network namespace, reach each other on
  `localhost`). Publish ports on the `.pod`, never on its member containers.
- Publish only what the host needs, and bind to an address when possible
  (`PublishPort=127.0.0.1:8080:8080`) instead of all interfaces.
- Rootless networking is pasta (Podman 5 default). The host is reachable from a container as
  `host.containers.internal`.

**Ordering and health**
- `After=`/`Requires=` order *process start*, not readiness. For a dependency others need ready
  (databases, brokers), add `HealthCmd=` and `Notify=healthy` so systemd reports it started
  only once the healthcheck passes. Cover slow init with `HealthStartPeriod=`.
- Add `HealthOnFailure=kill` so an unhealthy container exits and systemd's `Restart=` brings it
  back. Without it, a failing healthcheck only changes a status field.
- Consumers should still retry connections at startup; dependencies restart independently.
- `Restart=always` (or `on-failure`) in `[Service]`. Set `StopTimeout=` in `[Container]` if the
  app needs longer than 10 s to shut down cleanly.

**Secrets and configuration**
- Credentials (passwords, tokens, keys, connection strings containing passwords) go in Podman
  secrets, not `Environment=` or `EnvironmentFile=`. Env files are plaintext on disk, are easy
  to commit, and show up in `podman inspect` and `systemctl show`.
- Env-var consumers: `Secret=db-password,type=env,target=POSTGRES_PASSWORD`.
  File consumers: `Secret=db-password,type=mount,target=/run/secrets/db-password,mode=0400`.
  Prefer `*_FILE` variants when the image supports them.
- Create secrets from stdin so the value stays out of argv and shell history, as the same user
  that runs the units (rootless secrets are per-user):
  `printf %s "$VALUE" | podman secret create db-password -`. Make setup scripts idempotent:
  `podman secret exists db-password || ...`. Generate credentials with `openssl rand`.
- Non-sensitive settings: `Environment=KEY=value`.
- Never write a real credential into a unit file, env file, or script you create.

**Storage and user namespaces**
- Prefer named volumes (`.volume` file, `Volume=app-data.volume:/var/lib/app`): Podman handles
  ownership, and there is no host path to create.
- For bind mounts, the rootless UID mapping matters. Container root is the user's UID; any other
  container UID maps into the subuid range, so files it writes aren't owned by the user. Pick one:
  - `UserNS=keep-id` (optionally `keep-id:uid=1000,gid=1000`) to map the user's UID onto the
    container's app user. Good when the user must also edit the files.
  - `:U` on the mount to chown the source into the container's mapping. This changes ownership
    on the host, so only point it at a directory dedicated to this container.
- On SELinux hosts, bind mounts need `:Z` (private to one container) or `:z` (shared). Never use
  either on home directories or system paths.
- Create bind-mount source directories before start: `ExecStartPre=mkdir -p %h/app/data`.
- Specifiers: `%h` home, `%t` runtime dir (`/run/user/UID`), `%N` unit name. Write `%%` for a
  literal `%`.

**Hardening** (add each one, then test; some images break)
- `NoNewPrivileges=true`
- `DropCapability=ALL` plus `AddCapability=` only what is required
- `ReadOnly=true` with `Tmpfs=/tmp` and volumes for paths that must be writable
- `User=` (the `[Container]` key, which sets the user inside the container, unlike the
  unsupported `[Service] User=`) if the image runs as root and doesn't need to
- Resource ceilings: `Memory=`, `PidsLimit=`

**Escaping**
- systemd expands `$VAR` and `%` in `Exec=` and `HealthCmd=`. Write `$$VAR` for a variable
  that the container's shell should expand: `HealthCmd=pg_isready -U $${POSTGRES_USER}`.

**Readability**
- `Description=` on every unit (it's what `systemctl status` shows).
- Set `ContainerName=` to the file's base name so the container, service, and DNS name agree.
- Comment anything non-obvious inline: why a port is published, why a timeout is long.

## Workflow

1. **Gather the source**: a `podman run` command, a compose file, or a description. `podlet`
   (`podlet podman run ...`, `podlet compose`) can produce a first draft if installed; review its
   output against this skill rather than accepting it as-is.
2. **Check the host** prerequisites above if you have access to it, or list them for the user.
3. **Write the units.** Start from [templates.md](templates.md). Typical set for an app with a
   database: `app.network`, `app-db.volume`, `app-db.container` (healthcheck,
   `Notify=healthy`), `app.container` (`After=`/`Requires=app-db.service`).
4. **Validate with the generator** before starting anything:
   ```bash
   /usr/libexec/podman/quadlet -dryrun -user    # or /usr/lib/podman/quadlet
   systemd-analyze --user --generators=true verify app.service
   ```
   The dry run reports unknown keys, bad references, and prints the generated units.
5. **Install and start:**
   ```bash
   systemctl --user daemon-reload
   systemctl --user start app.service
   ```
   On Podman 5.6+, `podman quadlet install <file-or-dir>` / `podman quadlet list` /
   `podman quadlet rm` manage files in the user's directory for you.
6. **Verify:**
   ```bash
   systemctl --user status app.service
   journalctl --user -u app.service -e
   podman ps
   podman healthcheck run app
   ```
7. **Persist:** confirm linger is enabled, then reboot (or log out and back in) once to check the
   units come back on their own.

## Debugging checklist

- **Unit missing after `daemon-reload`:** generator rejected it. Run the dry run, or
  `journalctl --user -b | grep -i quadlet`. Usual causes: misspelled key, wrong extension, file
  outside a search path, a key newer than the installed Podman.
- **`Unit app.service not found`:** wrong generated name (`.pod` gives `-pod.service`), or you
  ran `systemctl --user` as a different user than the file's owner.
- **`Failed to connect to bus` / `$XDG_RUNTIME_DIR not set`:** no proper user session. See
  host prerequisites.
- **Services gone after logout or reboot:** linger is off.
- **`systemctl --user enable` errors with "transient or generated":** expected; use
  `[Install] WantedBy=default.target` and daemon-reload.
- **Times out on first start:** image pull. Raise `TimeoutStartSec=` or pre-pull.
- **Permission denied on a bind mount:** SELinux label (`:Z`/`:z`) or UID mapping
  (`UserNS=keep-id`, `:U`). `podman unshare ls -ln <dir>` shows ownership as the container sees it.
- **Containers can't resolve each other:** not on the same user-defined network or pod, or the
  name used doesn't match `ContainerName=`.
- **Dependency started but wasn't ready:** add `HealthCmd=` + `Notify=healthy` on it.
- **Env value mangled:** `$` or `%` escaping.
- **Secret not found:** created as another user, or by root. `podman secret ls` as the unit's
  user.
- **See exactly what systemd runs:** `systemctl --user cat app.service`.

When reviewing existing units, compare them against this skill and the project's conventions,
and report deviations as suggestions with the reason, rather than rewriting working config
unasked.
