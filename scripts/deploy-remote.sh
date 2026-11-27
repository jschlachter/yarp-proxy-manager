#!/usr/bin/env bash
# Deploys the ProxyManager Quadlet units from systemd/ to a remote host over SSH.
#
# The remote layout mirrors systemd/: the proxymanager group (network, target, proxy) lands at the
# root of the Quadlet directory, every other service group lands in its own subdirectory.
#
#   ~/.config/containers/systemd/proxymanager/
#   ├── deploy.sh              <- systemd/proxymanager/deploy.sh
#   ├── services/              <- systemd/proxymanager/services/
#   ├── api/{deploy.sh,services/}
#   ├── files/{deploy.sh,services/}
#   └── ...
#
#   ~/proxymanager/proxymanager.env  <- systemd/proxymanager.env (if present; non-secret settings,
#                                       read by the units via EnvironmentFile=)
#
# Each group's own deploy.sh does the real work (creating Podman secrets, copying its units into
# the Quadlet directory), so this script only stages files and drives the sequence:
#
#   0. copy the app images (west94.com/..., pinned by the units' Image= lines) from the local
#      Podman store to the host with `podman save | ssh podman load`, skipping any the host already
#      has with the same image ID. There is no registry, so without this the units try to pull
#      west94.com/... and fail. Build them first with ./scripts/build-images.sh, which defaults
#      to the units' tag.
#   1. stop proxymanager.target if it is active (and a pod left by earlier versions)
#   2. copy the proxymanager group to the Quadlet directory root
#   3. copy every other service group to <root>/<svc>/
#   4. run <root>/deploy.sh
#   5. run <root>/<svc>/deploy.sh for each service group
#   6. systemctl --user daemon-reload
#   7. start proxymanager.target (pulls in every container unit)
#
# Requirements on the remote host: bash, tar, podman, and a systemd user session for the target
# user (`loginctl enable-linger <user>` so the units survive logout).
#
# Usage:
#   ./scripts/deploy-remote.sh [user@]host [options]
#
# Options:
#   --port=N          SSH port (default: 22)
#   --identity=FILE   SSH private key
#   --quadlet-dir=D   Remote Quadlet directory, relative to $HOME unless absolute
#                     (default: .config/containers/systemd/proxymanager)
#   --stage-dir=D     Where the group folders (deploy.sh + services/) are copied, relative to
#                     $HOME unless absolute (default: the Quadlet directory). Quadlet recurses
#                     into subdirectories of its search path, so with the default every unit is
#                     also seen at <root>/<svc>/services/ — identical content, so it generates
#                     the same service, but a unit deleted from the root would come back from
#                     its staging copy. Stage outside the search path to avoid that.
#   --only=a,b,c      Only stage/deploy these service groups (the proxymanager group is always
#                     staged, since it owns the network and the target)
#   --no-restart      Stage and deploy, but leave the stack stopped
#   --no-images       Don't copy the app images (the host already has them, e.g. via a registry)
#   --dry-run         Print what would happen without touching the remote host
#
# Environment:
#   REMOTE_HOST       Alternative to passing the host positionally
#   SSH_OPTS          Extra options appended to every ssh invocation
#   OIDC_CLIENT_SECRET  Authentik client secret for the proxy; used only when the host has no
#                       proxymanager-oidc-client-secret yet. If unset you are prompted.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SYSTEMD_DIR="$REPO_ROOT/systemd"

# The group that owns the network and the target; its files live at the Quadlet directory root.
ROOT_GROUP="proxymanager"
# Groups every container unit (WantedBy=/PartOf=); start/stop it to start/stop the whole stack.
STACK_TARGET="proxymanager.target"
# Earlier versions ran everything in this pod; stopped here, removed by proxymanager/deploy.sh.
LEGACY_POD_SERVICE="proxymanager-pod.service"
# Issued by Authentik, so proxymanager/deploy.sh can't generate it; created here if missing.
OIDC_SECRET_NAME="proxymanager-oidc-client-secret"

REMOTE_HOST="${REMOTE_HOST:-}"
SSH_PORT=22
SSH_IDENTITY=""
QUADLET_DIR=".config/containers/systemd/proxymanager"
STAGE_DIR=""
ONLY=""
RESTART=1
DRY_RUN=0
COPY_IMAGES=1

for arg in "$@"; do
    case "$arg" in
        --port=*) SSH_PORT="${arg#--port=}" ;;
        --identity=*) SSH_IDENTITY="${arg#--identity=}" ;;
        --quadlet-dir=*) QUADLET_DIR="${arg#--quadlet-dir=}" ;;
        --stage-dir=*) STAGE_DIR="${arg#--stage-dir=}" ;;
        --only=*) ONLY="${arg#--only=}" ;;
        --no-restart) RESTART=0 ;;
        --no-images) COPY_IMAGES=0 ;;
        --dry-run) DRY_RUN=1 ;;
        -h|--help)
            awk 'NR==1 {next} /^#/ {sub(/^# ?/, ""); print; next} {exit}' "${BASH_SOURCE[0]}"
            exit 0
            ;;
        -*) echo "Unknown option: $arg" >&2; exit 1 ;;
        *)
            if [ -n "$REMOTE_HOST" ]; then
                echo "Unexpected argument: $arg" >&2
                exit 1
            fi
            REMOTE_HOST="$arg"
            ;;
    esac
done

if [ -z "$REMOTE_HOST" ]; then
    echo "Error: no remote host given. Usage: ./scripts/deploy-remote.sh [user@]host" >&2
    exit 1
fi

STAGE_DIR="${STAGE_DIR:-$QUADLET_DIR}"

if [ ! -d "$SYSTEMD_DIR" ]; then
    echo "Error: $SYSTEMD_DIR not found." >&2
    exit 1
fi

SSH_ARGS=(-p "$SSH_PORT")
[ -n "$SSH_IDENTITY" ] && SSH_ARGS+=(-i "$SSH_IDENTITY")
# shellcheck disable=SC2206 # deliberate word splitting of caller-supplied options
[ -n "${SSH_OPTS:-}" ] && SSH_ARGS+=(${SSH_OPTS})

# Discover service groups: any systemd/<name>/ holding a services/ directory.
SERVICE_GROUPS=()
for dir in "$SYSTEMD_DIR"/*/; do
    name="$(basename "$dir")"
    [ -d "$dir/services" ] || continue
    [ "$name" = "$ROOT_GROUP" ] && continue
    if [ -n "$ONLY" ] && ! printf '%s' ",$ONLY," | grep -q ",$name,"; then
        continue
    fi
    SERVICE_GROUPS+=("$name")
done

if [ ! -d "$SYSTEMD_DIR/$ROOT_GROUP/services" ]; then
    echo "Error: $SYSTEMD_DIR/$ROOT_GROUP/services not found — the network and target live there." >&2
    exit 1
fi

# Sanity-check --only against what was actually found.
if [ -n "$ONLY" ]; then
    IFS=',' read -r -a requested <<< "$ONLY"
    for name in "${requested[@]}"; do
        if [ -z "$name" ] || [ "$name" = "$ROOT_GROUP" ]; then
            continue
        fi
        if ! printf '%s\n' "${SERVICE_GROUPS[@]:-}" | grep -qx "$name"; then
            echo "Error: unknown service group '$name' (no $SYSTEMD_DIR/$name/services)." >&2
            exit 1
        fi
    done
fi

# App images the deployed groups run: west94.com/... Image= lines, which build-images.sh produces.
# Third-party images (docker.io/...) are left to Quadlet to pull.
APP_IMAGES=()
if [ "$COPY_IMAGES" -eq 1 ]; then
    while IFS= read -r img; do
        [ -n "$img" ] && APP_IMAGES+=("$img")
    done < <(for name in "$ROOT_GROUP" "${SERVICE_GROUPS[@]:-}"; do
                 [ -n "$name" ] || continue
                 grep -h '^Image=west94\.com/' "$SYSTEMD_DIR/$name"/services/*.container 2>/dev/null \
                     | sed 's/^Image=//' || true   # groups like postgresql pin no app image
             done | sort -u)
fi

echo "==> Host:        $REMOTE_HOST (port $SSH_PORT)"
echo "==> Quadlet dir: $QUADLET_DIR"
[ "$STAGE_DIR" = "$QUADLET_DIR" ] || echo "==> Stage dir:   $STAGE_DIR"
echo "==> Groups:      $ROOT_GROUP (root) ${SERVICE_GROUPS[*]:-}"
echo

if [ "$DRY_RUN" -eq 1 ]; then
    echo "Dry run — nothing will be copied or executed. Planned steps:"
    echo "  -. check for an env file (here or on the host); create $OIDC_SECRET_NAME if missing"
    if [ "$COPY_IMAGES" -eq 1 ]; then
        for img in "${APP_IMAGES[@]:-}"; do
            [ -n "$img" ] && echo "  0. copy image $img (skipped if the host has the same ID)"
        done
    else
        echo "  0. skipped (--no-images)"
    fi
    echo "  1. stop $STACK_TARGET (and legacy $LEGACY_POD_SERVICE) if active"
    echo "  2. copy systemd/$ROOT_GROUP/ -> ~/$STAGE_DIR/"
    [ -f "$SYSTEMD_DIR/proxymanager.env" ] \
        && echo "     copy systemd/proxymanager.env -> ~/proxymanager/proxymanager.env"
    for name in "${SERVICE_GROUPS[@]:-}"; do
        [ -n "$name" ] && echo "  3. copy systemd/$name/ -> ~/$STAGE_DIR/$name/"
    done
    echo "  4. run ~/$STAGE_DIR/deploy.sh"
    echo "  5. run ~/$STAGE_DIR/<svc>/deploy.sh for each group"
    echo "  6. systemctl --user daemon-reload"
    [ "$RESTART" -eq 1 ] && echo "  7. start $STACK_TARGET (pulls in every container unit)" \
        || echo "  7. skipped (--no-restart)"
    exit 0
fi

ssh_exec() {
    ssh "${SSH_ARGS[@]}" "$REMOTE_HOST" "$@"
}

# Streams a local directory into a remote one via tar, so we do not depend on rsync being
# installed on the host. Existing files are overwritten; unrelated files are left alone.
copy_dir() {
    local src="$1" dest="$2"
    ssh_exec "mkdir -p '$dest'"
    # COPYFILE_DISABLE/--no-xattrs: macOS tar otherwise adds AppleDouble ._* files, which Quadlet
    # tries to parse as units; a bad ._x.container even knocks out the real x.container.
    COPYFILE_DISABLE=1 tar --no-xattrs -C "$src" -cf - . | ssh_exec "tar -C '$dest' -xf -"
}

copy_file() {
    local src="$1" dest="$2"
    ssh_exec "mkdir -p \"\$(dirname '$dest')\""
    < "$src" ssh_exec "cat > '$dest'"
}

echo "==> Verifying remote prerequisites..."
ssh_exec "command -v podman >/dev/null || { echo 'podman not found on remote host' >&2; exit 1; }
          command -v tar >/dev/null || { echo 'tar not found on remote host' >&2; exit 1; }
          systemctl --user show-environment >/dev/null 2>&1 || {
              echo 'no systemd user session — run: loginctl enable-linger \$USER' >&2; exit 1; }"

# --- Preconditions of proxymanager/deploy.sh -----------------------------------------------------
# Checked here, before step 1 stops the stack, so a missing input fails the deploy with the stack
# still running instead of leaving it down halfway through step 4.

# The env file: proxymanager/deploy.sh stops without one, and step 2 only copies a local copy.
if [ ! -f "$SYSTEMD_DIR/proxymanager.env" ] \
    && ! ssh_exec "test -f proxymanager/proxymanager.env"; then
    echo "Error: no systemd/proxymanager.env here and no ~/proxymanager/proxymanager.env on $REMOTE_HOST." >&2
    echo "       Copy systemd/proxymanager.env.example to systemd/proxymanager.env and fill it in" >&2
    echo "       (non-secret values only)." >&2
    exit 1
fi

# The OIDC client secret: proxymanager/deploy.sh would prompt for it, but over ssh it has no
# terminal, so create it here. The value goes over stdin, never on a command line.
if ssh_exec "podman secret exists '$OIDC_SECRET_NAME'"; then
    echo "==> Secret $OIDC_SECRET_NAME exists on the host."
else
    secret="${OIDC_CLIENT_SECRET:-}"
    if [ -z "$secret" ] && [ -t 0 ]; then
        read -rsp "Authentik OIDC client secret for the proxy: " secret
        echo
    fi
    if [ -z "$secret" ]; then
        echo "Error: no $OIDC_SECRET_NAME on $REMOTE_HOST; set OIDC_CLIENT_SECRET or run interactively." >&2
        exit 1
    fi
    echo "==> Creating $OIDC_SECRET_NAME on the host..."
    printf %s "$secret" | ssh_exec "podman secret create '$OIDC_SECRET_NAME' - >/dev/null"
    unset secret
fi

# --- 0. Copy app images ------------------------------------------------------------------------
# Before stopping the stack, so a slow upload doesn't extend the downtime.
if [ "${#APP_IMAGES[@]}" -gt 0 ]; then
    echo "==> Copying app images..."
    for img in "${APP_IMAGES[@]}"; do
        if ! podman image exists "$img"; then
            echo "Error: image $img is not in the local Podman store." >&2
            echo "       Build it with ./scripts/build-images.sh (defaults to the units' tag)." >&2
            exit 1
        fi
    done

    remote_arch="$(ssh_exec "uname -m")"
    case "$remote_arch" in
        x86_64) remote_arch=amd64 ;;
        aarch64) remote_arch=arm64 ;;
    esac

    for img in "${APP_IMAGES[@]}"; do
        local_id="$(podman image inspect --format '{{.Id}}' "$img")"
        local_arch="$(podman image inspect --format '{{.Architecture}}' "$img")"
        if [ "$local_arch" != "$remote_arch" ]; then
            echo "Error: $img is $local_arch but $REMOTE_HOST is $remote_arch." >&2
            echo "       Build it for $remote_arch (podman build --platform=linux/$remote_arch) before deploying." >&2
            exit 1
        fi
        remote_id="$(ssh_exec "podman image inspect --format '{{.Id}}' '$img' 2>/dev/null || true")"
        if [ "$remote_id" = "$local_id" ]; then
            echo "    $img — already on host, skipping"
            continue
        fi
        echo "    $img — sending"
        # -C: the archive is uncompressed, and these images compress well.
        podman save "$img" | ssh -C "${SSH_ARGS[@]}" "$REMOTE_HOST" "podman load"
    done
fi

# --- 1. Stop the stack ------------------------------------------------------------------------
# Every container unit has PartOf=proxymanager.target, so stopping the target stops them all. The
# services are named too because `systemctl stop` only waits for the units it was given; with the
# target alone it returns while the containers are still shutting down.
echo "==> Stopping $STACK_TARGET (if active)..."
ssh_exec "if systemctl --user is-active --quiet '$LEGACY_POD_SERVICE'; then
              echo '    $LEGACY_POD_SERVICE active — stopping'
              systemctl --user stop '$LEGACY_POD_SERVICE'
          fi
          if systemctl --user is-active --quiet '$STACK_TARGET'; then
              echo '    $STACK_TARGET active — stopping'
              systemctl --user stop '$STACK_TARGET' 'proxymanager*.service'
          fi"

# --- 2/3. Stage files -------------------------------------------------------------------------
echo "==> Copying $ROOT_GROUP group to ~/$STAGE_DIR/..."
copy_dir "$SYSTEMD_DIR/$ROOT_GROUP" "$STAGE_DIR"

# Copied straight to where the units read it; the root deploy.sh then finds no local copy and keeps
# this one. Without either, that deploy.sh stops with instructions.
if [ -f "$SYSTEMD_DIR/proxymanager.env" ]; then
    echo "==> Copying proxymanager.env to ~/proxymanager/proxymanager.env..."
    copy_file "$SYSTEMD_DIR/proxymanager.env" "proxymanager/proxymanager.env"
    ssh_exec "chmod 600 proxymanager/proxymanager.env"
else
    echo "==> No systemd/proxymanager.env — keeping the host's existing copy, if any."
fi

for name in "${SERVICE_GROUPS[@]:-}"; do
    [ -n "$name" ] || continue
    echo "==> Copying $name group to ~/$STAGE_DIR/$name/..."
    copy_dir "$SYSTEMD_DIR/$name" "$STAGE_DIR/$name"
done

# --- 4/5/6. Run the deploy scripts ------------------------------------------------------------
echo "==> Running deploy scripts on the remote host..."
ssh_exec "set -euo pipefail
          cd \"\$HOME/$STAGE_DIR\"
          # Each group's deploy.sh honours QUADLET_DIR; point them all at the same directory so
          # the units land in one flat place regardless of where the groups were staged.
          case '$QUADLET_DIR' in
              /*) export QUADLET_DIR='$QUADLET_DIR' ;;
              *)  export QUADLET_DIR=\"\$HOME/$QUADLET_DIR\" ;;
          esac
          mkdir -p \"\$QUADLET_DIR\"

          if [ -f ./deploy.sh ]; then
              echo '--- deploy.sh (root)'
              chmod +x ./deploy.sh
              ./deploy.sh
          else
              echo '--- no deploy.sh at the Quadlet directory root — skipping'
          fi

          for dir in ./*/; do
              [ -f \"\$dir/deploy.sh\" ] || continue
              echo \"--- \$(basename \"\$dir\")/deploy.sh\"
              chmod +x \"\$dir/deploy.sh\"
              (cd \"\$dir\" && ./deploy.sh)
          done

          echo '--- systemctl --user daemon-reload'
          systemctl --user daemon-reload"

# --- 7. Start the stack -----------------------------------------------------------------------
if [ "$RESTART" -eq 0 ]; then
    echo
    echo "==> --no-restart given; stack left stopped. Start it with:"
    echo "    ssh $REMOTE_HOST systemctl --user start $STACK_TARGET"
    exit 0
fi

# Each container unit has WantedBy=proxymanager.target, so starting the target starts all of them,
# ordered by their own After=/Requires= (database and broker first, once healthy).
echo "==> Starting $STACK_TARGET (pulls in every container unit)..."
ssh_exec "set -euo pipefail
          systemctl --user start '$STACK_TARGET'
          echo
          systemctl --user list-units 'proxymanager*' --no-legend --all"

echo
echo "==> Done. Logs for a single service:"
echo "    ssh $REMOTE_HOST journalctl --user -u proxymanager-api.service -f"
