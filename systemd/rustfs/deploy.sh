#!/usr/bin/env bash
# Deploys the RustFS Quadlet units on this host.
#
# Run as the user that runs the containers (rootless). Copies services/* into the Quadlet directory and
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
#
# Hex only: base64 can produce '/', and the access key travels in the SigV4 Credential field
# (AKID/date/region/service/...), which the server splits on '/'. Hex also keeps the access key at
# 20 characters, the common S3 limit. Values aren't printed; the files unit reads the secrets. To
# see one on this host: podman secret inspect --showsecret --format '{{.SecretData}}' <name>
create_secret() { # name, bytes-of-randomness (hex => 2 chars per byte), label
    if podman secret exists "$1"; then
        echo "==> Secret $1 already exists — skipping."
    else
        openssl rand -hex "$2" | tr -d '\n' | podman secret create "$1" -
        echo "==> Created $1 ($3)."
    fi
}

create_secret rfs-access-key 10 "RustFS access key, 20 chars"
create_secret rfs-secret-key 20 "RustFS secret key, 40 chars"

echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
