#!/usr/bin/env bash
# Uploads the static proxy-manager.west94.io certificate as a Manual Certificate
# resource, restoring TLS for ProxyManager's own edge hostname now that the
# static Kestrel:Https:Sni config block has been replaced by LettuceEncrypt-driven
# SNI selection (see docs/lets-encrypt-plan.md, "ProxyManager's own edge hostname").
#
# Flow (matches how the UI/API already handle certificate uploads):
#   1. POST the .pfx to ProxyManager.Files as a staged "certificate" asset.
#   2. POST /api/certificates with that asset id — CreateCertificateHandler commits
#      the staged asset and creates the Certificate aggregate row itself.
#
# Config is read from scripts/.env (see scripts/.env.example) — the same file
# scripts/get-token.sh uses — then overridable via environment variables of the
# same name:
#   AUTHENTIK_AUTHORITY      - OIDC issuer (only needed if ACCESS_TOKEN is not set)
#   AUTHENTIK_CLIENT_ID      - client_credentials client id (only needed if ACCESS_TOKEN is not set)
#   AUTHENTIK_CLIENT_SECRET  - client secret for that client (only needed if ACCESS_TOKEN is not set)
#   AUTHENTIK_SCOPE          - optional, space-separated scopes
#   FILES_URL                - base URL of ProxyManager.Files (default: https://localhost:5002)
#   API_URL                  - base URL of ProxyManager.API (default: https://localhost:5001)
#   CERT_PATH                - path to the .pfx to upload (default: src/ProxyManager/certs/proxy-manager.west94.io.pfx)
#   CERT_NAME                - Certificate name (default: proxy-manager.west94.io)
#   CERT_PASSPHRASE           - PFX passphrase, if the file is password-protected (optional)
#   ACCESS_TOKEN             - pre-fetched bearer token; skips the token_endpoint call entirely
#
# Usage:
#   ./scripts/upload-proxy-manager-certificate.sh
#   CERT_PATH=/path/to/other.pfx CERT_NAME=other.example.com ./scripts/upload-proxy-manager-certificate.sh
#   ACCESS_TOKEN=eyJ... FILES_URL=https://proxy.example.com API_URL=https://proxy.example.com ./scripts/upload-proxy-manager-certificate.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

FILES_URL="${FILES_URL:-https://localhost:5002}"
API_URL="${API_URL:-https://localhost:5001}"
CERT_PATH="${CERT_PATH:-$REPO_ROOT/src/ProxyManager/certs/proxy-manager.west94.io.pfx}"
CERT_NAME="${CERT_NAME:-proxy-manager.west94.io}"
CERT_PASSPHRASE="${CERT_PASSPHRASE:-}"

for cmd in curl jq; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "error: '$cmd' is required but not found on PATH" >&2
    exit 1
  fi
done

if [[ ! -f "$CERT_PATH" ]]; then
  echo "error: certificate file not found at '$CERT_PATH' (set CERT_PATH)" >&2
  exit 1
fi

if [[ -z "${ACCESS_TOKEN:-}" ]]; then
  : "${AUTHENTIK_AUTHORITY:?Set AUTHENTIK_AUTHORITY in scripts/.env or the environment, or set ACCESS_TOKEN directly (see scripts/.env.example)}"
  : "${AUTHENTIK_CLIENT_ID:?Set AUTHENTIK_CLIENT_ID in scripts/.env or the environment, or set ACCESS_TOKEN directly}"
  : "${AUTHENTIK_CLIENT_SECRET:?Set AUTHENTIK_CLIENT_SECRET in scripts/.env or the environment, or set ACCESS_TOKEN directly}"

  echo "Fetching access token via ${SCRIPT_DIR}/get-token.sh ..." >&2
  ACCESS_TOKEN="$("$SCRIPT_DIR/get-token.sh")"
fi

if [[ -z "$ACCESS_TOKEN" ]]; then
  echo "error: no access token available (get-token.sh returned empty)" >&2
  exit 1
fi

echo "Uploading '$CERT_PATH' to ${FILES_URL}/files (assetType=certificate) ..." >&2
upload_response=$(curl -fsS \
  -X POST "${FILES_URL}/files?assetType=certificate" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -F "file=@${CERT_PATH};type=application/x-pkcs12") || {
  echo "error: upload to ${FILES_URL}/files failed" >&2
  exit 1
}

asset_id=$(jq -r '.id // empty' <<<"$upload_response")
if [[ -z "$asset_id" ]]; then
  echo "error: no 'id' in upload response from ${FILES_URL}/files" >&2
  echo "$upload_response" | jq . >&2 || echo "$upload_response" >&2
  exit 1
fi
echo "Staged file asset: $asset_id" >&2

echo "Creating Certificate resource '$CERT_NAME' via ${API_URL}/api/certificates ..." >&2
create_payload=$(jq -n \
  --arg name "$CERT_NAME" \
  --arg certificateAssetId "$asset_id" \
  --arg passPhrase "$CERT_PASSPHRASE" \
  '{name: $name, format: "Pfx", certificateAssetId: $certificateAssetId, keyAssetId: null} +
   (if $passPhrase != "" then {passPhrase: $passPhrase} else {} end)')

create_response=$(curl -fsS \
  -X POST "${API_URL}/api/certificates" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$create_payload") || {
  echo "error: certificate creation at ${API_URL}/api/certificates failed" >&2
  echo "Staged asset '$asset_id' was uploaded but not committed — it will be swept as an orphan." >&2
  exit 1
}

cert_id=$(jq -r '.id // empty' <<<"$create_response")
if [[ -z "$cert_id" ]]; then
  echo "error: no 'id' in certificate creation response" >&2
  echo "$create_response" | jq . >&2 || echo "$create_response" >&2
  exit 1
fi

echo >&2
echo "Certificate created: $cert_id" >&2
echo "$create_response" | jq .
