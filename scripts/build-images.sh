#!/usr/bin/env bash
# Builds Podman container images for ProxyManager services.
# Build and publish happens within the container build process (multi-stage Containerfile).
#
# Usage:
#   ./scripts/build-images.sh [proxy|api|files|ui|all] [--tag=TAG]
#
# Arguments:
#   proxy     Build only ProxyManager (default: all)
#   api       Build only ProxyManager.API (default: all)
#   files     Build only ProxyManager.Files (default: all)
#   ui        Build only ProxyManager.UI (default: all)
#   all       Build all services (default)
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
BUILD_FILES=true
BUILD_UI=true

for arg in "$@"; do
    case "$arg" in
        proxy)    BUILD_PROXY=true;  BUILD_API=false; BUILD_FILES=false; BUILD_UI=false ;;
        api)      BUILD_PROXY=false; BUILD_API=true;  BUILD_FILES=false; BUILD_UI=false ;;
        files)    BUILD_PROXY=false; BUILD_API=false; BUILD_FILES=true;  BUILD_UI=false ;;
        ui)       BUILD_PROXY=false; BUILD_API=false; BUILD_FILES=false; BUILD_UI=true  ;;
        all)      BUILD_PROXY=true;  BUILD_API=true;  BUILD_FILES=true;  BUILD_UI=true  ;;
        --tag=*)  TAG="${arg#--tag=}" ;;
        *)        echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

PROXY_IMAGE="west94.com/proxymanager:${TAG}"
API_IMAGE="west94.com/proxymanager-api:${TAG}"
FILES_IMAGE="west94.com/proxymanager-files:${TAG}"
UI_IMAGE="west94.com/proxymanager-ui:${TAG}"

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

if $BUILD_FILES; then
    echo "==> Building container image: ${FILES_IMAGE}"
    podman build \
        -f src/ProxyManager.Files/Containerfile \
        -t "$FILES_IMAGE" \
        .
    echo "    Built: ${FILES_IMAGE}"
    echo
fi

if $BUILD_UI; then
    echo "==> Building container image: ${UI_IMAGE}"
    podman build \
        -f src/ProxyManager.UI/Containerfile \
        -t "$UI_IMAGE" \
        src/ProxyManager.UI
    echo "    Built: ${UI_IMAGE}"
    echo
fi

echo "==> Done! Images built:"
$BUILD_PROXY && podman image inspect --format "    {{.Repository}}:{{.Tag}}  ({{.Size}} bytes)" "$PROXY_IMAGE"
$BUILD_API   && podman image inspect --format "    {{.Repository}}:{{.Tag}}  ({{.Size}} bytes)" "$API_IMAGE"
$BUILD_FILES && podman image inspect --format "    {{.Repository}}:{{.Tag}}  ({{.Size}} bytes)" "$FILES_IMAGE"
$BUILD_UI    && podman image inspect --format "    {{.Repository}}:{{.Tag}}  ({{.Size}} bytes)" "$UI_IMAGE"
