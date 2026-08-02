#!/usr/bin/env bash
# Build ClickerFixer.Satellite as a self-contained, single-file linux-arm64
# binary, copy it to the Raspberry Pi, and restart the service.
#
# Usage: scripts/deploy-satellite.sh
set -euo pipefail

# EDIT ME — set to your Pi, e.g. "pi@192.168.1.42"
PI_HOST="pi@PI_IP_ADDRESS"

REMOTE_DIR="/opt/clicker-fixer-app"
SERVICE="clicker.service"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/ClickerFixer.Satellite/ClickerFixer.Satellite.csproj"
RID="linux-arm64"
CONFIGURATION="Release"
OUT_DIR="$REPO_ROOT/dist/satellite"

if [[ "$PI_HOST" == *PI_IP_ADDRESS* ]]; then
    echo "ERROR: set PI_HOST at the top of this script before running it." >&2
    exit 1
fi

echo "==> Cleaning $OUT_DIR"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "==> Publishing $PROJECT ($RID, self-contained, single-file)"
dotnet publish "$PROJECT" \
    -c "$CONFIGURATION" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT_DIR"

echo "==> Copying to $PI_HOST:$REMOTE_DIR"
# Copies publish output into REMOTE_DIR. app.yml (the runtime config file
# living only on the Pi) is never part of the publish output, so it is
# never touched by this copy.
ssh "$PI_HOST" "sudo mkdir -p '$REMOTE_DIR' && sudo chown \$(whoami) '$REMOTE_DIR'"
scp -r "$OUT_DIR"/* "$PI_HOST:$REMOTE_DIR/"

echo "==> Restarting $SERVICE"
ssh "$PI_HOST" "sudo systemctl daemon-reload && sudo systemctl restart '$SERVICE'"

echo "==> Status"
ssh "$PI_HOST" "sudo systemctl status '$SERVICE' --no-pager -l" || true

echo "==> Recent logs"
ssh "$PI_HOST" "sudo journalctl -u '$SERVICE' -n 30 --no-pager" || true

echo "==> Done"
