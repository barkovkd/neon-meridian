#!/bin/bash
# Tools/unity.sh <log-file> [Unity args…]
#
# Runs the Unity editor in batch mode on this project and writes the editor log to <log-file>.
#
# Unity cannot work on an exFAT / FAT volume: its asset database needs a real local file system, and the
# AppleDouble "._*" files macOS writes on such volumes break the build. When the project sits on one, the
# editor runs on a work copy on the internal disk instead: sources are copied there before the run, and
# whatever Unity generated or changed (.meta files, settings, assets made by the setup script) is copied
# back afterwards. On APFS / HFS the editor simply runs in place.
#
# Settings, from the environment or from Tools/local.env (machine-specific, not in version control;
# see Tools/local.env.example):
#   NEON_WORK_DIR        where the work copy lives (default: ~/Library/Caches/NeonMeridian/Project)
#   UNITY                path to the Unity editor binary
#   MACHINE_LOCK_SCRIPT  a "one heavy job at a time" wrapper to queue behind, called as: <script> <who> <command…>
#   NO_MACHINE_LOCK      set to skip that wrapper for one run
SRC="$(cd "$(dirname "$0")/.." && pwd)"
if [ -f "$SRC/Tools/local.env" ]; then . "$SRC/Tools/local.env"; fi
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity}"
LOG="$1"
if [ -z "$LOG" ]; then echo "usage: Tools/unity.sh <log-file> [Unity args…]"; exit 2; fi
shift

# Queue behind the machine lock when this machine has one (one Unity / one game run at a time), then re-enter.
LOCK_SCRIPT="${MACHINE_LOCK_SCRIPT:-}"
if [ -n "$LOCK_SCRIPT" ] && [ -f "$LOCK_SCRIPT" ] && [ -z "$NO_MACHINE_LOCK" ] && [ -z "$NEON_INSIDE_LOCK" ]; then
  NEON_INSIDE_LOCK=1 exec bash "$LOCK_SCRIPT" neon-meridian bash "$0" "$LOG" "$@"
fi

fstype() {
  local dev
  dev="$(df "$1" | tail -1 | awk '{print $1}')"
  mount | awk -v dev="$dev" '$1 == dev' | sed -E 's/.*\(([a-z0-9_]+).*/\1/' | head -1
}

FS="$(fstype "$SRC")"
case "$FS" in
  apfs|hfs) PROJECT="$SRC"; MIRROR=0 ;;
  *)        PROJECT="${NEON_WORK_DIR:-$HOME/Library/Caches/NeonMeridian/Project}"; MIRROR=1 ;;
esac
echo "sources: $SRC ($FS)"
echo "project: $PROJECT"

if [ "$MIRROR" = 1 ]; then
  mkdir -p "$PROJECT"
  for d in Assets Packages ProjectSettings; do
    rsync -a --delete --modify-window=2 --exclude '._*' --exclude '.DS_Store' "$SRC/$d/" "$PROJECT/$d/" \
      || { echo "copying $d to the work copy failed"; exit 90; }
  done
fi

"$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$LOG" "$@"
RC=$?

if [ "$MIRROR" = 1 ]; then
  # bring back what Unity created or changed; never overwrite something edited here in the meantime
  for d in Assets Packages ProjectSettings; do
    rsync -au --modify-window=2 --exclude '._*' --exclude '.DS_Store' "$PROJECT/$d/" "$SRC/$d/"
  done
fi

echo "unity rc=$RC"
exit $RC
