#!/usr/bin/env bash
# Deploys the UI Quadlet units on this host.
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

echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
