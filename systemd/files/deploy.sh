#!/usr/bin/env bash
# Deploys the files service Quadlet units on this host.
#
# Run as the user that runs the containers (rootless). Creates the files service token secret if missing,
# copies services/* into the Quadlet directory and reloads systemd so the generator picks them up.
# Safe to re-run.
#
# The token secret (proxymanager-files-service-token) is shared: the files service verifies it and
# the API and proxy send it (X-Files-Service-Token), so deploy this before starting either of them.
#
# Usage:
#   ./deploy.sh
#
# Environment:
#   QUADLET_DIR   Destination (default: ~/.config/containers/systemd/proxymanager)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
QUADLET_DIR="${QUADLET_DIR:-$HOME/.config/containers/systemd/proxymanager}"

# Podman secrets are per-user. Only create when missing; rotating it means restarting the API,
# proxy and files service together.
if podman secret exists proxymanager-files-service-token; then
    echo "==> Secret proxymanager-files-service-token already exists — skipping."
else
    echo "==> Creating files service token secret..."
    openssl rand -hex 32 | tr -d '\n' | podman secret create proxymanager-files-service-token -
fi

echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
