#!/usr/bin/env bash
# Deploys ProxyManager systemd/Quadlet units to the default Podman Desktop VM.
#
# Copies all Quadlet unit files and .env from the systemd/ directory into
# ~/.config/containers/systemd/proxymanager/ on the VM. If TLS certificates
# exist under src/ProxyManager/certs/ they are copied to ~/proxymanager/certs/
# (the path mounted into the container). Then triggers systemctl --user
# daemon-reload so the Quadlet generator picks up the new units.
#
# Usage:
#   ./scripts/deploy-vm.sh [--machine=NAME]
#
# Options:
#   --machine=NAME   Podman machine name (default: podman-machine-default)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SYSTEMD_DIR="$REPO_ROOT/systemd"
CERTS_DIR="$REPO_ROOT/src/ProxyManager/certs"

MACHINE="${MACHINE:-podman-machine-default}"
REMOTE_DIR=".config/containers/systemd/proxymanager"
REMOTE_CERTS_DIR="proxymanager/certs"
REMOTE_CONFIG_DIR="proxymanager/config"

for arg in "$@"; do
    case "$arg" in
        --machine=*) MACHINE="${arg#--machine=}" ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

ssh_exec() {
    podman machine ssh "$MACHINE" -- "$@"
}

copy_file() {
    local src="$1"
    local dest_dir="$2"
    echo "    $(basename "$src")"
    < "$src" podman machine ssh "$MACHINE" -- bash -c "cat > '$dest_dir/$(basename "$src")'"
}

echo "==> Machine: $MACHINE"
echo "==> Remote:  ~/$REMOTE_DIR"
echo

# Verify the machine is running
if ! podman machine inspect "$MACHINE" --format '{{.State}}' 2>/dev/null | grep -q "running"; then
    echo "Error: machine '$MACHINE' is not running." >&2
    echo "Start it with: podman machine start $MACHINE" >&2
    exit 1
fi

echo "==> Creating remote directory..."
ssh_exec mkdir -p "$REMOTE_DIR"

echo "==> Copying unit files..."
for f in "$SYSTEMD_DIR"/*.container \
          "$SYSTEMD_DIR"/*.pod \
          "$SYSTEMD_DIR"/*.network \
          "$SYSTEMD_DIR"/*.volume \
          "$SYSTEMD_DIR"/.env; do
    [ -f "$f" ] && copy_file "$f" "$REMOTE_DIR"
done

echo
echo "==> Copying certificates..."
if compgen -G "$CERTS_DIR/*" > /dev/null 2>&1; then
    ssh_exec mkdir -p "$REMOTE_CERTS_DIR"
    for f in "$CERTS_DIR"/*; do
        [ -f "$f" ] && copy_file "$f" "$REMOTE_CERTS_DIR"
    done
else
    echo "    No certificates found in src/ProxyManager/certs/ — skipping."
fi

echo "==> Copying config files..."
ssh_exec mkdir -p "$REMOTE_CONFIG_DIR"
if compgen -G "$REPO_ROOT/proxysettings.*.json" > /dev/null 2>&1; then
    for f in "$REPO_ROOT/proxysettings.*.json"; do
        [ -f "$f" ] && copy_file "$f" "$REMOTE_CONFIG_DIR"
    done
else
    echo "    No config files found in config/ — skipping."
fi

echo
echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
ssh_exec systemctl --user daemon-reload

echo
echo "==> Units available in the VM:"
ssh_exec systemctl --user list-unit-files "proxymanager*" --no-legend 2>/dev/null || true

echo
echo "==> Done! To start the pod, run:"
echo "    podman pod start pod-proxymanager.service"
