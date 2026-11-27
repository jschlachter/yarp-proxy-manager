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
#   --tag=TAG Image tag to apply (default: the tag the Quadlet units pin in their Image= lines,
#             or value of TAG env var)
#
# The Quadlet units in systemd/*/services/ are the source of truth for the deployed tag (Image=
# can't take variables), so the default build tag is read from them and a build always produces
# what the units will run. Bump the tag in the units, not here. A different --tag is still
# allowed (e.g. a scratch build), but the units won't run it.
#
# Examples:
#   ./scripts/build-images.sh
#   ./scripts/build-images.sh proxy --tag=1.2.0
#   TAG=dev ./scripts/build-images.sh api

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Every west94.com app image pinned by a unit, as "repo:tag" lines.
unit_images() {
    grep -h '^Image=west94\.com/' "$REPO_ROOT"/systemd/*/services/*.container | sed 's/^Image=//'
}

# The single tag the units agree on; fails if they disagree, so the build can't silently match
# only some of them.
unit_tag() {
    local tags
    tags="$(unit_images | sed 's/.*://' | sort -u)"
    if [ -z "$tags" ]; then
        echo "Error: no west94.com Image= lines found under systemd/*/services/." >&2
        return 1
    fi
    if [ "$(printf '%s\n' "$tags" | wc -l)" -ne 1 ]; then
        echo "Error: the Quadlet units pin different image tags:" >&2
        unit_images | sed 's/^/    /' >&2
        echo "       Make the Image= lines in systemd/*/services/*.container agree." >&2
        return 1
    fi
    printf '%s' "$tags"
}

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

if [ -z "${TAG:-}" ]; then
    TAG="$(unit_tag)"
fi

PROXY_IMAGE="west94.com/proxymanager:${TAG}"
API_IMAGE="west94.com/proxymanager-api:${TAG}"
FILES_IMAGE="west94.com/proxymanager-files:${TAG}"
UI_IMAGE="west94.com/proxymanager-ui:${TAG}"

echo "==> Working directory: $REPO_ROOT"
echo "==> Image tag: ${TAG}"
if [ "$TAG" != "$(unit_tag 2>/dev/null || true)" ]; then
    echo "    Note: the Quadlet units pin a different tag; they won't run these images."
fi
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
# `podman image inspect` has no .Repository/.Tag fields; `podman images` does. if-blocks rather than
# `$BUILD_X && ...` so a skipped last image doesn't make the script exit non-zero.
show_image() {
    podman images --format "    {{.Repository}}:{{.Tag}}  ({{.Size}})" --filter "reference=$1"
}
if $BUILD_PROXY; then show_image "$PROXY_IMAGE"; fi
if $BUILD_API;   then show_image "$API_IMAGE"; fi
if $BUILD_FILES; then show_image "$FILES_IMAGE"; fi
if $BUILD_UI;    then show_image "$UI_IMAGE"; fi
