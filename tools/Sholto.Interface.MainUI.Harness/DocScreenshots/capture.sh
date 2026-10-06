#!/usr/bin/env bash
# Re-takes the README/docs screenshots with the real app renderer (MainUI harness, headless).
#   bash tools/Sholto.Interface.MainUI.Harness/DocScreenshots/capture.sh [WORKDIR] [HERO_THEME]
# Needs ffmpeg (makes a silent, tagged demo library from tracks.tsv) and python3 with Pillow.
# Writes pictures/*.webp; look at every image before committing it.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
WORK="${1:-$(mktemp -d)}"
HERO="${2:-The Birthday Massacre}"
mkdir -p "$WORK/library"
while IFS=$'\t' read -r artist title seconds _bpm _key; do
  f="$WORK/library/$artist - $title.mp3"
  [ -f "$f" ] || ffmpeg -nostdin -loglevel error -f lavfi -i anullsrc=r=22050:cl=mono -t "$seconds" \
    -c:a libmp3lame -b:a 8k -metadata artist="$artist" -metadata title="$title" "$f"
done < "$HERE/tracks.tsv"
python3 "$HERE/scenarios.py" "$WORK" "$HERO"
dotnet build "$ROOT/tools/Sholto.Interface.MainUI.Harness" -nologo -v q -m:2 -nodeReuse:false -p:UseSharedCompilation=false
for s in "$WORK"/sc/*.json; do
  dotnet run --no-build --project "$ROOT/tools/Sholto.Interface.MainUI.Harness" -- ui --scenario "$s" > "${s%.json}.out"
done
python3 "$HERE/compose.py" "$WORK" "$ROOT/pictures"
echo "raw captures in $WORK/raw"
