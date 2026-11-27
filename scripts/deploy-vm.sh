#!/usr/bin/env bash
# Deploys the ProxyManager Quadlet units to a local Podman machine (Podman Desktop VM).
#
# Mirrors scripts/deploy-remote.sh, but talks to the VM through `podman machine ssh`:
#
#   1. check that the app images the units pin (west94.com/...:<tag>) are in the VM's rootless
#      image store. The default Podman connection on macOS is that store, so
#      ./scripts/build-images.sh puts them there; nothing is copied.
#   2. create the proxy's OIDC client secret in the VM if it is missing (prompting here, since
#      `podman machine ssh` gives the remote deploy.sh no terminal to prompt on)
#   3. stage systemd/<svc>/ (deploy.sh + services/) and systemd/proxymanager.env to
#      ~/proxymanager/deploy/ in the VM, outside the Quadlet directory, so Quadlet doesn't see
#      every unit twice
#   4. run proxymanager/deploy.sh (network, proxymanager.target, env file) and then every other <svc>/deploy.sh,
#      which create their secrets and install their units into the Quadlet directory
#   5. copy TLS certificates from src/ProxyManager/certs/ to ~/proxymanager/certs/ (the proxy's
#      bind mount), if any
#   6. optionally restart proxymanager.target so changed units and images take effect
#
# Usage:
#   ./scripts/deploy-vm.sh [--machine=NAME] [--restart]
#
# Options:
#   --machine=NAME   Podman machine name (default: podman-machine-default, or $MACHINE)
#   --restart        (Re)start the stack after deploying. Without it, it is left as it was.
#
# Environment:
#   OIDC_CLIENT_SECRET  Authentik client secret for the proxy; used only when the VM has no
#                       proxymanager-oidc-client-secret yet. If unset you are prompted.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SYSTEMD_DIR="$REPO_ROOT/systemd"
CERTS_DIR="$REPO_ROOT/src/ProxyManager/certs"

# The group that owns the network, the target and the env file; its deploy.sh must run first.
ROOT_GROUP="proxymanager"
STACK_TARGET="proxymanager.target"
OIDC_SECRET_NAME="proxymanager-oidc-client-secret"

MACHINE="${MACHINE:-podman-machine-default}"
# Relative to the VM user's home.
STAGE_DIR="proxymanager/deploy"
REMOTE_CERTS_DIR="proxymanager/certs"
RESTART=0

for arg in "$@"; do
    case "$arg" in
        --machine=*) MACHINE="${arg#--machine=}" ;;
        --restart) RESTART=1 ;;
        -h|--help)
            awk 'NR==1 {next} /^#/ {sub(/^# ?/, ""); print; next} {exit}' "${BASH_SOURCE[0]}"
            exit 0
            ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

# Runs a command line in the VM. Like ssh, the arguments are joined and re-parsed by the remote
# shell, so quote anything that isn't a plain word (printf %q).
vm() {
    podman machine ssh "$MACHINE" -- "$@"
}

# Service groups: any systemd/<name>/ with a services/ directory, the root group first.
SERVICE_GROUPS=("$ROOT_GROUP")
for dir in "$SYSTEMD_DIR"/*/; do
    name="$(basename "$dir")"
    [ -d "$dir/services" ] && [ "$name" != "$ROOT_GROUP" ] && SERVICE_GROUPS+=("$name")
done
if [ ! -d "$SYSTEMD_DIR/$ROOT_GROUP/services" ]; then
    echo "Error: $SYSTEMD_DIR/$ROOT_GROUP/services not found — the network and target live there." >&2
    exit 1
fi

echo "==> Machine: $MACHINE"
echo "==> Groups:  ${SERVICE_GROUPS[*]}"
echo

if ! podman machine inspect "$MACHINE" --format '{{.State}}' 2>/dev/null | grep -q "running"; then
    echo "Error: machine '$MACHINE' is not running." >&2
    echo "Start it with: podman machine start $MACHINE" >&2
    exit 1
fi

# --- 1. Images -----------------------------------------------------------------------------------
# Quadlet's default Pull=missing uses a local image as-is, but would try to pull west94.com/...
# (not a real registry) if it's absent, and the unit would fail.
echo "==> Checking app images in the VM..."
# Collected first: `podman machine ssh` reads stdin, so it can't run inside a `while read` loop.
app_images=()
while IFS= read -r img; do
    [ -n "$img" ] && app_images+=("$img")
done < <(grep -h '^Image=west94\.com/' "$SYSTEMD_DIR"/*/services/*.container | sed 's/^Image=//' | sort -u)
missing=()
for img in "${app_images[@]}"; do
    if vm podman image exists "$(printf %q "$img")" < /dev/null; then
        echo "    $img"
    else
        echo "    $img — MISSING"
        missing+=("$img")
    fi
done
if [ "${#missing[@]}" -gt 0 ]; then
    echo "Error: the VM's rootless image store lacks the image(s) above." >&2
    echo "       Build them with ./scripts/build-images.sh (defaults to the units' tag). If your" >&2
    echo "       default Podman connection is the rootful one, switch with:" >&2
    echo "       podman system connection default $MACHINE" >&2
    exit 1
fi

# --- 2. OIDC client secret -----------------------------------------------------------------------
# proxymanager/deploy.sh creates it from OIDC_CLIENT_SECRET or a prompt, but it has no terminal
# here; create it directly instead. The value goes over stdin, never on a command line.
if vm podman secret exists "$OIDC_SECRET_NAME"; then
    echo "==> Secret $OIDC_SECRET_NAME exists in the VM."
else
    secret="${OIDC_CLIENT_SECRET:-}"
    if [ -z "$secret" ] && [ -t 0 ]; then
        read -rsp "Authentik OIDC client secret for the proxy: " secret
        echo
    fi
    if [ -z "$secret" ]; then
        echo "Error: no $OIDC_SECRET_NAME in the VM; set OIDC_CLIENT_SECRET or run interactively." >&2
        exit 1
    fi
    echo "==> Creating $OIDC_SECRET_NAME in the VM..."
    printf %s "$secret" | vm podman secret create "$OIDC_SECRET_NAME" -
    unset secret
fi

# --- 3. Stage ------------------------------------------------------------------------------------
# Same layout as systemd/, so proxymanager/deploy.sh finds the env file at ../proxymanager.env.
# Wiped first so files deleted from the repo don't linger in the staging copy.
echo "==> Staging ${SERVICE_GROUPS[*]} to ~/$STAGE_DIR/..."
stage_items=("${SERVICE_GROUPS[@]}")
if [ -f "$SYSTEMD_DIR/proxymanager.env" ]; then
    stage_items+=(proxymanager.env)
else
    echo "    No systemd/proxymanager.env — the VM's existing ~/proxymanager/proxymanager.env is kept;"
    echo "    without one, proxymanager/deploy.sh stops (template: systemd/proxymanager.env.example)."
fi
vm rm -rf "$STAGE_DIR" '&&' mkdir -p "$STAGE_DIR"
COPYFILE_DISABLE=1 tar --no-xattrs -C "$SYSTEMD_DIR" -cf - "${stage_items[@]}" \
    | vm tar -C "$STAGE_DIR" -xf -
vm chmod 600 "$STAGE_DIR/proxymanager.env" 2>/dev/null || true

# --- 4. Run each group's deploy.sh ---------------------------------------------------------------
# Each one creates its own secrets, copies its units into the Quadlet directory and reloads
# systemd. The script goes over stdin; the group list is quoted for the remote shell.
echo "==> Running deploy scripts in the VM..."
vm bash -s -- "$(printf '%q ' "$STAGE_DIR" "${SERVICE_GROUPS[@]}")" <<'EOF'
set -euo pipefail
stage="$HOME/$1"; shift
for group in "$@"; do
    echo "--- $group/deploy.sh"
    chmod +x "$stage/$group/deploy.sh"
    (cd "$stage/$group" && ./deploy.sh)
done
EOF

# --- 5. Certificates -----------------------------------------------------------------------------
echo "==> Copying certificates..."
cert_files=()
if [ -d "$CERTS_DIR" ]; then
    for f in "$CERTS_DIR"/*; do
        [ -f "$f" ] && cert_files+=("$(basename "$f")")
    done
fi
if [ "${#cert_files[@]}" -gt 0 ]; then
    vm mkdir -p "$REMOTE_CERTS_DIR"
    COPYFILE_DISABLE=1 tar --no-xattrs -C "$CERTS_DIR" -cf - "${cert_files[@]}" \
        | vm tar -C "$REMOTE_CERTS_DIR" -xf -
    printf '    %s\n' "${cert_files[@]}"
else
    echo "    No certificates found in src/ProxyManager/certs/ — skipping."
fi

# --- 6. Start / report ---------------------------------------------------------------------------
echo
echo "==> Units available in the VM:"
vm systemctl --user list-unit-files "'proxymanager*'" --no-legend 2>/dev/null || true

echo
if [ "$RESTART" -eq 1 ]; then
    # Stop, then start: every container unit is PartOf= the target (so the stop reaches all of them)
    # and WantedBy= it (so the start brings all of them up, in their After=/Requires= order). The
    # services are named in the stop because `systemctl stop` only waits for the units it was given.
    echo "==> Restarting $STACK_TARGET..."
    vm systemctl --user stop "$STACK_TARGET" "'proxymanager*.service'"
    vm systemctl --user start "$STACK_TARGET"
    vm systemctl --user list-units "'proxymanager*'" --no-legend --all
    echo
    echo "==> Done. Logs: podman machine ssh $MACHINE -- journalctl --user -u proxymanager-api.service -f"
else
    echo "==> Done! To start (or restart, to pick up changes) every service, run:"
    echo "    podman machine ssh $MACHINE -- systemctl --user restart $STACK_TARGET"
    echo "    or re-run with --restart."
fi
