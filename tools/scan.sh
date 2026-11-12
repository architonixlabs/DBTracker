#!/usr/bin/env bash
# Scans this repo with the shared Architonix code platform (SonarQube :7001, Dependency-Track :7002/:7003,
# DefectDojo :7004 on 192.168.1.8). The platform's scan-project.sh holds the URLs and reads its tokens from
# scan.env beside it; nothing secret lives in this repo.
#
#   bash tools/scan.sh                    # from the repo root, Git Bash or Linux
#   CODE_PLATFORM_DIR=/path bash tools/scan.sh
#
# Needs Docker running (every scanner runs as a throwaway container) and a LAN route to 192.168.1.8.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CODE_PLATFORM_DIR="${CODE_PLATFORM_DIR:-$REPO/../DockerSecurity/code-platform}"
SCRIPT="$CODE_PLATFORM_DIR/scan-project.sh"

if [ ! -f "$SCRIPT" ]; then
  echo "Can't find $SCRIPT. Set CODE_PLATFORM_DIR to the folder holding scan-project.sh and scan.env." >&2
  exit 1
fi
if ! docker info >/dev/null 2>&1; then
  echo "Docker isn't running. Start Docker Desktop (or the Docker service) and run this again." >&2
  exit 1
fi

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$REPO/src/DBTracker.Web/DBTracker.Web.csproj" | head -n1)"
export PROJECT_KEY="DBTracker"
export VERSION="${VERSION:-1.0.0}"
# The platform's defaults (bin, obj, out, publish, .git, .vs ...) plus this repo's generated and local-only folders.
export EXCLUDE_DIRS="${EXCLUDE_DIRS:-bin obj out publish TestResults .git .vs .vscode .claude .scan-reports node_modules}"

bash "$SCRIPT" "$REPO"
