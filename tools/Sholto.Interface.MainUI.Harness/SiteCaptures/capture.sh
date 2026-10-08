#!/usr/bin/env bash
# Regenerates the marketing site's animations and stills from the real app renderer (MainUI harness, headless).
#   bash tools/Sholto.Interface.MainUI.Harness/SiteCaptures/capture.sh [WORKDIR] [SCENARIO...]
# Needs ffmpeg and python3 with Pillow. Writes site/assets/*. Look at every output before committing it.
# The harness is shared: waits until no other harness run is using it, then kills only the runs it started (by PID) on exit.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
WORK="${1:-$(mktemp -d)}"; shift || true
ONLY=("$@")
mkdir -p "$WORK/library"
while IFS=$'\t' read -r artist title seconds _bpm _key; do
  f="$WORK/library/$artist - $title.mp3"
  [ -f "$f" ] || ffmpeg -nostdin -loglevel error -f lavfi -i anullsrc=r=22050:cl=mono -t "$seconds" \
    -c:a libmp3lame -b:a 8k -metadata artist="$artist" -metadata title="$title" "$f"
done < "$HERE/../DocScreenshots/tracks.tsv"
python3 "$HERE/scenarios.py" "$WORK"
# Wait for the shared harness, ignoring this script and the shell that launched it (their command lines match too).
others() { for p in $(pgrep -f "$1"); do [ "$p" = "$$" ] || [ "$p" = "$PPID" ] || return 0; done; return 1; }
while others "[S]holto.Interface.MainUI.Harness|[S]holto.dll"; do sleep 10; done
while others "^(/\S*/)?dotnet (build|test)"; do sleep 5; done
dotnet build "$ROOT/tools/Sholto.Interface.MainUI.Harness" -nologo -v q -m:2 -nodeReuse:false -p:UseSharedCompilation=false
# dll-not-rebuilding trap (CLAUDE.md): show the dll stamps so a stale build is visible
stat -c 'dll %y %n' "$ROOT"/src/Sholto.Interface.MainUI/bin/Debug/net10.0/Sholto.dll \
                    "$ROOT"/tools/Sholto.Interface.MainUI.Harness/bin/Debug/net10.0/Sholto.dll 2>/dev/null || true
# Kill only the process tree of the run this script started (by PID); never match other Sholto processes.
RUNPID=""
killtree() { local c; for c in $(pgrep -P "$1" 2>/dev/null); do killtree "$c"; done; kill -9 "$1" 2>/dev/null || true; }
trap '[ -n "$RUNPID" ] && killtree "$RUNPID"; true' EXIT
for s in "$WORK"/sc/*.json; do
  name="$(basename "$s" .json)"
  if [ ${#ONLY[@]} -gt 0 ] && [[ ! " ${ONLY[*]} " == *" $name "* ]]; then continue; fi
  rm -f "$WORK/raw/$name"-*.png
  dotnet run --no-build --project "$ROOT/tools/Sholto.Interface.MainUI.Harness" -- ui --scenario "$s" > "${s%.json}.out" &
  RUNPID=$!
  wait "$RUNPID"
  RUNPID=""
done
python3 "$HERE/encode.py" "$WORK" "$ROOT/site/assets"
echo "raw captures in $WORK/raw"
