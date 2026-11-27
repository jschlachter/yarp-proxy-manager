#!/usr/bin/env bash
# Deploys the RabbitMQ Quadlet units on this host.
#
# Run as the user that runs the containers (rootless). Creates the broker credentials as Podman secrets if
# missing, copies services/* into the Quadlet directory and reloads systemd so the generator picks
# them up. Safe to re-run: existing credentials are never regenerated.
#
# Secrets (all created only when missing):
#   proxymanager-rabbitmq-user      broker username; env RabbitMQ__UserName in proxy, API, Files
#   proxymanager-rabbitmq-password  generated password; env RabbitMQ__Password in proxy, API, Files
#   proxymanager-rabbitmq-conf      rabbitmq.conf fragment (default_user/default_pass) mounted into
#                                   the broker at /etc/rabbitmq/conf.d/; RabbitMQ recommends the
#                                   config file over RABBITMQ_DEFAULT_USER/PASS outside dev and CI
#
# RabbitMQ creates the default user only on first boot, while its data volume is still empty. A
# broker that already booted keeps its existing users (e.g. guest); see the note this script prints.
#
# Usage:
#   ./deploy.sh [--username=NAME]
#
# Options:
#   --username=NAME  Broker username (default: $RABBITMQ_USERNAME, else "proxymanager"). Used
#                    only when the user secret doesn't exist yet; asking for a different name than
#                    the existing secret is an error, since the broker won't rename its user.
#
# Environment:
#   QUADLET_DIR        Destination (default: ~/.config/containers/systemd/proxymanager)
#   RABBITMQ_USERNAME  Alternative to --username

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
QUADLET_DIR="${QUADLET_DIR:-$HOME/.config/containers/systemd/proxymanager}"

USER_SECRET="proxymanager-rabbitmq-user"
PASS_SECRET="proxymanager-rabbitmq-password"
CONF_SECRET="proxymanager-rabbitmq-conf"
DATA_VOLUME="proxymanager-rabbitmq-data"   # VolumeName= in proxymanager-rabbitmq.volume
DEFAULT_USERNAME="proxymanager"

USERNAME="${RABBITMQ_USERNAME:-}"
for arg in "$@"; do
    case "$arg" in
        --username=*) USERNAME="${arg#--username=}" ;;
        -h|--help)
            awk 'NR==1 {next} /^#/ {sub(/^# ?/, ""); print; next} {exit}' "${BASH_SOURCE[0]}"
            exit 0
            ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done
USERNAME_GIVEN=0
[ -n "$USERNAME" ] && USERNAME_GIVEN=1
USERNAME="${USERNAME:-$DEFAULT_USERNAME}"

# Letters, digits, '.', '_' and '-' only: the name goes into rabbitmq.conf (where '#' starts a
# comment) and into AMQP URIs (where ':', '@' and '/' are delimiters).
if ! [[ "$USERNAME" =~ ^[A-Za-z0-9._-]{1,64}$ ]]; then
    echo "Error: invalid username '$USERNAME' (use 1-64 of A-Z a-z 0-9 . _ -)." >&2
    exit 1
fi
if [ "$USERNAME" = "guest" ]; then
    echo "Error: 'guest' is RabbitMQ's well-known default account; pick another name." >&2
    exit 1
fi

read_secret() {
    podman secret inspect --showsecret --format '{{.SecretData}}' "$1"
}

created_any=0

# --- Username ------------------------------------------------------------------------------------
if podman secret exists "$USER_SECRET"; then
    existing_user="$(read_secret "$USER_SECRET")"
    if [ "$USERNAME_GIVEN" -eq 1 ] && [ "$existing_user" != "$USERNAME" ]; then
        echo "Error: $USER_SECRET is already '$existing_user'; refusing to switch to '$USERNAME'." >&2
        echo "       The broker keeps the user it was first booted with. To start over, stop the stack (systemctl --user stop proxymanager.target)," >&2
        echo "       then: podman secret rm $USER_SECRET $PASS_SECRET $CONF_SECRET" >&2
        echo "             podman volume rm $DATA_VOLUME" >&2
        exit 1
    fi
    USERNAME="$existing_user"
    echo "==> Secret $USER_SECRET already exists ($USERNAME) — skipping."
else
    printf %s "$USERNAME" | podman secret create "$USER_SECRET" - >/dev/null
    echo "==> Created $USER_SECRET ($USERNAME)."
    created_any=1
fi

# --- Password ------------------------------------------------------------------------------------
# Hex only: safe in rabbitmq.conf ('#' would start a comment) and in AMQP URIs. 32 bytes = 64 chars.
if podman secret exists "$PASS_SECRET"; then
    echo "==> Secret $PASS_SECRET already exists — skipping."
else
    openssl rand -hex 32 | tr -d '\n' | podman secret create "$PASS_SECRET" - >/dev/null
    echo "==> Created $PASS_SECRET (64 hex chars)."
    created_any=1
fi

# --- Broker config fragment ----------------------------------------------------------------------
# Derived from the two secrets above, so it's rebuilt whenever either was just created. Values aren't
# printed; to see them: podman secret inspect --showsecret --format '{{.SecretData}}' <name>
if [ "$created_any" -eq 1 ] && podman secret exists "$CONF_SECRET"; then
    podman secret rm "$CONF_SECRET" >/dev/null
fi
if podman secret exists "$CONF_SECRET"; then
    echo "==> Secret $CONF_SECRET already exists — skipping."
else
    printf 'default_user = %s\ndefault_pass = %s\n' "$USERNAME" "$(read_secret "$PASS_SECRET")" \
        | podman secret create "$CONF_SECRET" - >/dev/null
    echo "==> Created $CONF_SECRET (rabbitmq.conf fragment)."
fi

# --- Existing broker data ------------------------------------------------------------------------
if [ "$created_any" -eq 1 ] && podman volume exists "$DATA_VOLUME"; then
    cat >&2 <<EOF
WARNING: $DATA_VOLUME already exists, so RabbitMQ has booted before and won't create user
         '$USERNAME'; the proxy, API and Files services will fail to log in until it exists.
         Either reset the broker (queues and exchanges are re-declared by the services on start;
         messages still queued are lost):
             systemctl --user stop proxymanager.target 'proxymanager*.service'
             podman volume rm $DATA_VOLUME
         or add the user to the running broker and remove guest:
             podman secret inspect --showsecret --format '{{.SecretData}}' $PASS_SECRET \\
                 | podman exec -i proxymanager-rabbitmq rabbitmqctl add_user '$USERNAME'
             podman exec proxymanager-rabbitmq rabbitmqctl set_user_tags '$USERNAME' administrator
             podman exec proxymanager-rabbitmq rabbitmqctl set_permissions -p / '$USERNAME' '.*' '.*' '.*'
             podman exec proxymanager-rabbitmq rabbitmqctl delete_user guest
EOF
fi

# --- Units ---------------------------------------------------------------------------------------
echo "==> Installing units to $QUADLET_DIR..."
mkdir -p "$QUADLET_DIR"
cp "$SCRIPT_DIR"/services/* "$QUADLET_DIR"/

echo "==> Reloading systemd user daemon (triggers Quadlet generator)..."
systemctl --user daemon-reload
