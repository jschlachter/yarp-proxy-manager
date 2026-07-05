#!/usr/bin/env bash
# Wraps dotnet-outdated for this solution: report -> (optional) upgrade -> build -> test.
#
# Usage:
#   driver.sh report                    # list outdated packages, no changes
#   driver.sh upgrade [--majors]        # upgrade minor/patch only by default; --majors allows major bumps
#   driver.sh verify                    # dotnet build + dotnet test on the whole solution
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

SLN="ProxyManager.sln"

cmd="${1:-report}"

case "$cmd" in
  report)
    dotnet outdated "$SLN"
    ;;
  upgrade)
    if [[ "${2:-}" == "--majors" ]]; then
      dotnet outdated "$SLN" -u
    else
      dotnet outdated "$SLN" -vl Major -u
    fi
    ;;
  verify)
    dotnet build "$SLN"
    dotnet test "$SLN" --filter "Category=Unit"
    ;;
  *)
    echo "Unknown command: $cmd (expected report|upgrade|verify)" >&2
    exit 1
    ;;
esac
