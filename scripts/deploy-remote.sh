#!/usr/bin/env bash
# Deploys the ProxyManager Quadlet units from systemd/ to a remote host over SSH.
#
# The remote layout mirrors systemd/: the proxymanager group (pod, network, proxy) lands at the
# root of the Quadlet directory, every other service group lands in its own subdirectory.
#
#   ~/.config/containers/systemd/proxymanager/
#   ├── deploy.sh              <- systemd/proxymanager/deploy.sh
#   ├── services/              <- systemd/proxymanager/services/
#   ├── .env                   <- systemd/.env (if present)
#   ├── api/{deploy.sh,services/}
#   ├── files/{deploy.sh,services/}
#   └── ...
#
# Each group's own deploy.sh does the real work (creating Podman secrets, copying its units into
# the Quadlet directory), so this script only stages files and drives the sequence:
#
#   1. stop the pod service if it is running
#   2. copy the proxymanager group to the Quadlet directory root
#   3. copy every other service group to <root>/<svc>/
#   4. run <root>/deploy.sh
#   5. run <root>/<svc>/deploy.sh for each service group
#   6. systemctl --user daemon-reload
#   7. start the pod service (Quadlet makes it pull in the member containers)
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
#                     staged, since it owns the pod and network)
#   --no-restart      Stage and deploy, but leave the pod stopped
#   --dry-run         Print what would happen without touching the remote host
#
# Environment:
#   REMOTE_HOST       Alternative to passing the host positionally
#   SSH_OPTS          Extra options appended to every ssh invocation

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SYSTEMD_DIR="$REPO_ROOT/systemd"

# The group that owns the pod/network; its files live at the Quadlet directory root.
ROOT_GROUP="proxymanager"
POD_SERVICE="proxymanager-pod.service"

REMOTE_HOST="${REMOTE_HOST:-}"
SSH_PORT=22
SSH_IDENTITY=""
QUADLET_DIR=".config/containers/systemd/proxymanager"
STAGE_DIR=""
ONLY=""
RESTART=1
DRY_RUN=0

for arg in "$@"; do
    case "$arg" in
        --port=*) SSH_PORT="${arg#--port=}" ;;
        --identity=*) SSH_IDENTITY="${arg#--identity=}" ;;
        --quadlet-dir=*) QUADLET_DIR="${arg#--quadlet-dir=}" ;;
        --stage-dir=*) STAGE_DIR="${arg#--stage-dir=}" ;;
        --only=*) ONLY="${arg#--only=}" ;;
        --no-restart) RESTART=0 ;;
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
    echo "Error: $SYSTEMD_DIR/$ROOT_GROUP/services not found — the pod and network live there." >&2
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

echo "==> Host:        $REMOTE_HOST (port $SSH_PORT)"
echo "==> Quadlet dir: $QUADLET_DIR"
[ "$STAGE_DIR" = "$QUADLET_DIR" ] || echo "==> Stage dir:   $STAGE_DIR"
echo "==> Groups:      $ROOT_GROUP (root) ${SERVICE_GROUPS[*]:-}"
echo

if [ "$DRY_RUN" -eq 1 ]; then
    echo "Dry run — nothing will be copied or executed. Planned steps:"
    echo "  1. stop $POD_SERVICE if active"
    echo "  2. copy systemd/$ROOT_GROUP/ -> ~/$STAGE_DIR/"
    [ -f "$SYSTEMD_DIR/.env" ] && echo "     copy systemd/.env -> ~/$QUADLET_DIR/.env"
    for name in "${SERVICE_GROUPS[@]:-}"; do
        [ -n "$name" ] && echo "  3. copy systemd/$name/ -> ~/$STAGE_DIR/$name/"
    done
    echo "  4. run ~/$STAGE_DIR/deploy.sh"
    echo "  5. run ~/$STAGE_DIR/<svc>/deploy.sh for each group"
    echo "  6. systemctl --user daemon-reload"
    [ "$RESTART" -eq 1 ] && echo "  7. start $POD_SERVICE (pulls in the member containers)" \
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
    tar -C "$src" -cf - . | ssh_exec "tar -C '$dest' -xf -"
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

# --- 1. Stop the pod service ------------------------------------------------------------------
# Member containers have BindsTo=<pod service>, so stopping the pod stops them too.
echo "==> Stopping $POD_SERVICE (if running)..."
ssh_exec "if systemctl --user is-active --quiet '$POD_SERVICE'; then
              echo '    active — stopping'
              systemctl --user stop '$POD_SERVICE'
          else
              echo '    not running — nothing to stop'
          fi"

# --- 2/3. Stage files -------------------------------------------------------------------------
echo "==> Copying $ROOT_GROUP group to ~/$STAGE_DIR/..."
copy_dir "$SYSTEMD_DIR/$ROOT_GROUP" "$STAGE_DIR"

if [ -f "$SYSTEMD_DIR/.env" ]; then
    echo "==> Copying .env to ~/$QUADLET_DIR/.env..."
    copy_file "$SYSTEMD_DIR/.env" "$QUADLET_DIR/.env"
else
    echo "==> No systemd/.env found — skipping (units referencing it will fail to start)."
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

# --- 7. Start the pod -------------------------------------------------------------------------
if [ "$RESTART" -eq 0 ]; then
    echo
    echo "==> --no-restart given; pod left stopped. Start it with:"
    echo "    ssh $REMOTE_HOST systemctl --user start $POD_SERVICE"
    exit 0
fi

# Quadlet gives the pod service Wants=/Before= for every member container (StartWithPod defaults
# to true), so starting the pod brings the whole set up — no per-container start needed.
echo "==> Starting $POD_SERVICE (pulls in the member containers)..."
ssh_exec "set -euo pipefail
          systemctl --user start '$POD_SERVICE'
          echo
          systemctl --user list-units 'proxymanager*' --no-legend --all"

echo
echo "==> Done. Logs for a single service:"
echo "    ssh $REMOTE_HOST journalctl --user -u proxymanager-api.service -f"
