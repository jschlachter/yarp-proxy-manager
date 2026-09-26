#!/usr/bin/env bash
# Deploys the PostgreSQL Quadlet units on this host.
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

# Podman secrets are per-user. Only create when missing so re-runs don't rotate the credential
# out from under an existing database volume.
if podman secret exists pm-db-pass; then
    echo "==> Secret pm-db-pass already exists — skipping."
else
    echo "==> Creating Postgres superuser password secret..."
    DB_PASS="$(openssl rand -base64 36 | tr -d '\n')"
    printf %s "$DB_PASS" | podman secret create pm-db-pass -
    echo "    Postgres password (shown once, store it somewhere safe): $DB_PASS"
fi

echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
