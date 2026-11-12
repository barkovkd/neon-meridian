#!/bin/bash
# Tools/build.sh — builds the macOS app and leaves it in Builds/ next to the project.
#
# The Unity editor is run in batch mode (see Tools/unity.sh). If the project sits on an exFAT / FAT volume the
# build happens in a work copy on the internal disk and the finished app is copied back here.
HERE="$(cd "$(dirname "$0")/.." && pwd)"
LOG="${NEON_BUILD_LOG:-${TMPDIR:-/tmp}/neon-meridian-build.log}"

echo "Building Neon Meridian… (editor log: $LOG)"
OUT="$(bash "$HERE/Tools/unity.sh" "$LOG" -executeMethod NeonMeridianSetup.BatchBuild 2>&1)"
RC=$?
echo "$OUT"
PROJECT="$(echo "$OUT" | sed -n 's/^project: //p' | head -1)"
APP="$PROJECT/Builds/Neon Meridian.app"

if [ $RC -ne 0 ] || [ ! -d "$APP" ]; then
  echo
  echo "Build failed. Errors from the editor log:"
  grep -E "error CS|Shader error|\[Setup\] FAILED|BuildFailedException" "$LOG" | sort -u | head -30
  exit 1
fi

if [ "$PROJECT" != "$HERE" ]; then
  mkdir -p "$HERE/Builds"
  rm -rf "$HERE/Builds/Neon Meridian.app"
  ditto --norsrc "$APP" "$HERE/Builds/Neon Meridian.app" || { echo "could not copy the app to $HERE/Builds"; exit 1; }
fi
echo
echo "Done: $HERE/Builds/Neon Meridian.app"
