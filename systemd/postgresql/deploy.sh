#!/usr/bin/env bash
# Deploys the PostgreSQL Quadlet units on this host.
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

# Podman secrets are per-user. Only create when missing so re-runs don't rotate the credential
# out from under an existing database volume.
if podman secret exists pm-db-pass; then
    echo "==> Secret pm-db-pass already exists — skipping."
else
    # Not printed: the API, proxy and files units read it from the secret. To see it on this host:
    #   podman secret inspect --showsecret --format '{{.SecretData}}' pm-db-pass
    echo "==> Creating Postgres superuser password secret..."
    openssl rand -base64 36 | tr -d '\n' | podman secret create pm-db-pass -
fi

echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
