#!/usr/bin/env bash
# Deploys the proxy (YARP) Quadlet units, plus the shared network and the proxymanager.target that
# groups every service, on this host.
#
# Run as the user that runs the containers (rootless). Installs the shared non-secret env file,
# creates the OIDC client secret if missing, installs and enables proxymanager.target, copies
# services/* into the Quadlet directory and reloads systemd so the generator picks them up. Removes
# the pod left by earlier versions. Safe to re-run.
#
# Usage:
#   ./deploy.sh
#
# Environment:
#   QUADLET_DIR         Destination (default: ~/.config/containers/systemd/proxymanager)
#   ENV_FILE            Non-secret settings to install (default: systemd/proxymanager.env;
#                       template: systemd/proxymanager.env.example)
#   OIDC_CLIENT_SECRET  Authentik client secret for the proxy, used only when the
#                       proxymanager-oidc-client-secret Podman secret doesn't exist yet. If unset
#                       and stdin is a terminal, you are prompted for it.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
QUADLET_DIR="${QUADLET_DIR:-$HOME/.config/containers/systemd/proxymanager}"
# The target is a plain systemd unit; Quadlet ignores .target files in its own directory.
SYSTEMD_USER_DIR="${SYSTEMD_USER_DIR:-$HOME/.config/systemd/user}"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/../proxymanager.env}"
# Read by the proxy, API, Files and UI units (EnvironmentFile=%h/proxymanager/proxymanager.env). Kept outside
# the Quadlet directory so it resolves the same wherever the unit files are.
ENV_DEST="$HOME/proxymanager/proxymanager.env"

# --- Non-secret settings -------------------------------------------------------------------------
if [ -f "$ENV_FILE" ]; then
    echo "==> Installing $ENV_FILE to $ENV_DEST..."
    mkdir -p "$(dirname "$ENV_DEST")"
    install -m 600 "$ENV_FILE" "$ENV_DEST"
elif [ -f "$ENV_DEST" ]; then
    echo "==> No $ENV_FILE — keeping existing $ENV_DEST."
else
    echo "Error: no env file at $ENV_FILE and none installed at $ENV_DEST." >&2
    echo "       Copy systemd/proxymanager.env.example to systemd/proxymanager.env and fill it in" >&2
    echo "       (non-secret values only)." >&2
    exit 1
fi

# --- Secrets -------------------------------------------------------------------------------------
# Podman secrets are per-user. The OIDC client secret is issued by Authentik, so it can't be
# generated here; take it from OIDC_CLIENT_SECRET or a prompt, only when the secret is missing.
if podman secret exists proxymanager-oidc-client-secret; then
    echo "==> Secret proxymanager-oidc-client-secret already exists — skipping."
else
    secret="${OIDC_CLIENT_SECRET:-}"
    if [ -z "$secret" ] && [ -t 0 ]; then
        read -rsp "Authentik OIDC client secret for the proxy: " secret
        echo
    fi
    if [ -z "$secret" ]; then
        echo "Error: Podman secret proxymanager-oidc-client-secret is missing." >&2
        echo "       Re-run with OIDC_CLIENT_SECRET set, or create it on this host with:" >&2
        echo "       printf %s '<client secret>' | podman secret create proxymanager-oidc-client-secret -" >&2
        exit 1
    fi
    echo "==> Creating OIDC client secret..."
    printf %s "$secret" | podman secret create proxymanager-oidc-client-secret -
    unset secret
fi

# --- Host prerequisites --------------------------------------------------------------------------
# The proxy container publishes host port 80 (HTTP and Let's Encrypt HTTP-01). Rootless Podman can
# only bind it if the host allows unprivileged binds that low; otherwise the proxy fails to start.
# Needs root to change, so only warn.
port_start="$(sysctl -n net.ipv4.ip_unprivileged_port_start 2>/dev/null || echo 1024)"
if [ "$port_start" -gt 80 ]; then
    echo "WARNING: net.ipv4.ip_unprivileged_port_start=$port_start, so the rootless proxy can't publish port 80." >&2
    echo "         As root: echo 'net.ipv4.ip_unprivileged_port_start=80' > /etc/sysctl.d/90-proxymanager.conf" >&2
    echo "                  sysctl --system" >&2
fi

# --- Retire the pod -------------------------------------------------------------------------------
# Earlier versions ran every service in proxymanager.pod. Stopping the pod service stops and removes
# the pod and its member containers; then every copy of the .pod file (including staged copies
# under the Quadlet directory) has to go, or Quadlet keeps generating the pod.
if find "$QUADLET_DIR" -name proxymanager.pod 2>/dev/null | grep -q .; then
    echo "==> Removing the old proxymanager pod..."
    systemctl --user stop proxymanager-pod.service 2>/dev/null || true
    find "$QUADLET_DIR" -name proxymanager.pod -print -delete | sed 's/^/    removed /'
fi

# --- Units ---------------------------------------------------------------------------------------
echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Installing proxymanager.target to $SYSTEMD_USER_DIR..."
mkdir -p "$SYSTEMD_USER_DIR"
cp "$SCRIPT_DIR/proxymanager.target" "$SYSTEMD_USER_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload

# Enabled so the stack starts at boot (with linger). The container units can't be enabled
# themselves; Quadlet turns their WantedBy=proxymanager.target into the target's wants.
echo "==> Enabling proxymanager.target..."
systemctl --user enable proxymanager.target
