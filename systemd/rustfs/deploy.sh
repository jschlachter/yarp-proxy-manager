#!/usr/bin/env bash
# Deploys the RustFS Quadlet units on this host.
#
# Run as the user that runs the pod (rootless). Copies services/* into the Quadlet directory and
# reloads systemd so the generator picks them up. Safe to re-run.
#
# Usage:
#   ./deploy.sh
#
# Environment:
#   QUADLET_DIR   Destination (default: ~/.config/containers/systemd/proxymanager)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
QUADLET_DIR="${QUADLET_DIR:-$HOME/.config/containers/systemd/proxymanager}"

# Podman secrets are per-user. Only create when missing so re-runs don't rotate credentials
# that the files service (and existing bucket data) already depend on.
create_secret() { # name, bytes-of-randomness, label
    if podman secret exists "$1"; then
        echo "==> Secret $1 already exists — skipping."
    else
        local value
        value="$(openssl rand -base64 "$2" | tr -d '\n')"
        printf %s "$value" | podman secret create "$1" -
        echo "==> Created $1 ($3, shown once, store it somewhere safe): $value"
    fi
}

create_secret rfs-access-key 36 "RustFS access key"
create_secret rfs-secret-key 60 "RustFS secret key"

echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
