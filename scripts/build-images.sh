#!/usr/bin/env bash
# Builds Podman container images for ProxyManager services.
# Build and publish happens within the container build process (multi-stage Containerfile).
#
# Usage:
#   ./scripts/build-images.sh [proxy|api|all] [--tag=TAG]
#
# Arguments:
#   proxy     Build only ProxyManager (default: all)
#   api       Build only ProxyManager.API (default: all)
#   all       Build both services (default)
#   --tag=TAG Image tag to apply (default: latest, or value of TAG env var)
#
# Examples:
#   ./scripts/build-images.sh
#   ./scripts/build-images.sh proxy --tag=1.2.0
#   TAG=dev ./scripts/build-images.sh api

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

TAG="${TAG:-latest}"
BUILD_PROXY=true
BUILD_API=true

for arg in "$@"; do
    case "$arg" in
        proxy)    BUILD_PROXY=true;  BUILD_API=false ;;
        api)      BUILD_PROXY=false; BUILD_API=true  ;;
        all)      BUILD_PROXY=true;  BUILD_API=true  ;;
        --tag=*)  TAG="${arg#--tag=}" ;;
        *)        echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

PROXY_IMAGE="west94.com/proxy-manager:${TAG}"
API_IMAGE="west94.com/proxy-api:${TAG}"

echo "==> Working directory: $REPO_ROOT"
echo "==> Image tag: ${TAG}"
echo

cd "$REPO_ROOT"

if $BUILD_PROXY; then
    echo "==> Building container image: ${PROXY_IMAGE}"
    podman build \
        -f src/ProxyManager/Containerfile \
        -t "$PROXY_IMAGE" \
        .
    echo "    Built: ${PROXY_IMAGE}"
    echo
fi

if $BUILD_API; then
    echo "==> Building container image: ${API_IMAGE}"
    podman build \
        -f src/ProxyManager.API/Containerfile \
        -t "$API_IMAGE" \
        .
    echo "    Built: ${API_IMAGE}"
    echo
fi

echo "==> Done! Images built:"
$BUILD_PROXY && podman image inspect --format "    {{.Repository}}:{{.Tag}}  ({{.Size}} bytes)" "$PROXY_IMAGE"
$BUILD_API   && podman image inspect --format "    {{.Repository}}:{{.Tag}}  ({{.Size}} bytes)" "$API_IMAGE"
