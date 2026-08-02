#!/usr/bin/env bash
# Build and publish ClickerFixer.Desktop as a self-contained, single-file
# win-x64 executable, zipped for distribution.
#
# Usage: scripts/publish-desktop.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/ClickerFixer.Desktop/ClickerFixer.Desktop.csproj"
RID="win-x64"
CONFIGURATION="Release"
OUT_DIR="$REPO_ROOT/dist/desktop"
VERSION="$(date +%Y%m%d-%H%M%S)"
ZIP_PATH="$REPO_ROOT/dist/ClickerFixer.Desktop-$RID-$VERSION.zip"

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

echo "==> Zipping to $ZIP_PATH"
mkdir -p "$REPO_ROOT/dist"
(cd "$OUT_DIR" && zip -r -q "$ZIP_PATH" .)

echo "==> Done"
echo "Published output: $OUT_DIR"
echo "Zip:              $ZIP_PATH"
