#!/usr/bin/env bash
# Fetches a JWT access token from Authentik (client_credentials grant) and
# prints it to the console for manual API testing (e.g. against Scalar or curl).
#
# Config is read from scripts/.env (see scripts/.env.example), then overridable
# via environment variables of the same name:
#   AUTHENTIK_AUTHORITY   - OIDC issuer, e.g. https://auth.west94.io/application/o/proxy-manager/
#   AUTHENTIK_CLIENT_ID   - client_credentials client id configured in Authentik
#   AUTHENTIK_CLIENT_SECRET - client secret for that client
#   AUTHENTIK_SCOPE       - optional, space-separated scopes (default: unset)
#
# Usage:
#   ./scripts/get-token.sh            # prints just the access token
#   ./scripts/get-token.sh --verbose  # also prints token type, expiry, and a ready-to-use curl example

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

: "${AUTHENTIK_AUTHORITY:?Set AUTHENTIK_AUTHORITY in scripts/.env or the environment (see scripts/.env.example)}"
: "${AUTHENTIK_CLIENT_ID:?Set AUTHENTIK_CLIENT_ID in scripts/.env or the environment (see scripts/.env.example)}"
: "${AUTHENTIK_CLIENT_SECRET:?Set AUTHENTIK_CLIENT_SECRET in scripts/.env or the environment (see scripts/.env.example)}"

VERBOSE=false
if [[ "${1:-}" == "--verbose" || "${1:-}" == "-v" ]]; then
  VERBOSE=true
fi

for cmd in curl jq; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "error: '$cmd' is required but not found on PATH" >&2
    exit 1
  fi
done

# Authentik's discovery document lives at "<authority>.well-known/openid-configuration"
# (authority already ends in "/"); resolve the token endpoint from it rather than
# hardcoding Authentik's URL shape.
DISCOVERY_URL="${AUTHENTIK_AUTHORITY%/}/.well-known/openid-configuration"

discovery=$(curl -fsS "$DISCOVERY_URL") || {
  echo "error: failed to fetch OIDC discovery document from $DISCOVERY_URL" >&2
  exit 1
}

token_endpoint=$(jq -r '.token_endpoint // empty' <<<"$discovery")
if [[ -z "$token_endpoint" ]]; then
  echo "error: discovery document at $DISCOVERY_URL had no token_endpoint" >&2
  exit 1
fi

token_args=(
  -fsS
  -X POST "$token_endpoint"
  -H "Content-Type: application/x-www-form-urlencoded"
  --data-urlencode "grant_type=client_credentials"
  --data-urlencode "client_id=$AUTHENTIK_CLIENT_ID"
  --data-urlencode "client_secret=$AUTHENTIK_CLIENT_SECRET"
)

if [[ -n "${AUTHENTIK_SCOPE:-}" ]]; then
  token_args+=(--data-urlencode "scope=$AUTHENTIK_SCOPE")
fi

response=$(curl "${token_args[@]}") || {
  echo "error: token request to $token_endpoint failed" >&2
  exit 1
}

access_token=$(jq -r '.access_token // empty' <<<"$response")
if [[ -z "$access_token" ]]; then
  echo "error: no access_token in response from $token_endpoint" >&2
  echo "$response" | jq . >&2 || echo "$response" >&2
  exit 1
fi

if [[ "$VERBOSE" == true ]]; then
  token_type=$(jq -r '.token_type // "Bearer"' <<<"$response")
  expires_in=$(jq -r '.expires_in // "unknown"' <<<"$response")

  echo "Token endpoint: $token_endpoint" >&2
  echo "Token type:     $token_type" >&2
  echo "Expires in:     ${expires_in}s" >&2
  echo >&2
  echo "Access token:" >&2
  echo "$access_token"
  echo >&2
  echo "Example usage:" >&2
  echo "  curl -H \"Authorization: Bearer $access_token\" https://localhost:5001/proxyhosts" >&2
else
  echo "$access_token"
fi
