#!/usr/bin/env bash
# Renders the backspin in backspin.json offline through Bench's headless host (no audio device)
# and prints the output WAV's path and frame count.
# Usage: tools/Sholto.Interface.Bench/Listen/render-backspin.sh <out.wav>
set -euo pipefail

readonly LISTEN_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd "$LISTEN_DIR/../../.." && pwd)"
readonly SCENARIO_TEMPLATE="$LISTEN_DIR/backspin.json"
readonly SOURCE_PLACEHOLDER="__SOURCE__"
# 44.1 kHz on purpose: a source at another rate than the 48 kHz engine is fully decoded on load, which
# gives the deck its in-memory scratch reader; a 48 kHz file would stream and could not scratch.
readonly SOURCE_RATE=44100
readonly SOURCE_SECONDS=8

# 120 BPM: a pitch-swept kick on every beat, a metallic hi-hat on every off-beat, an A minor sawtooth chord.
readonly BEAT='mod(t,0.5)'
readonly OFFBEAT='mod(t+0.25,0.5)'
saw() { echo "2*($1*t-floor($1*t+0.5))"; }
readonly KICK="0.55*sin(2*PI*(50*$BEAT+4*(1-exp(-30*$BEAT))))*exp(-9*$BEAT)"
readonly HAT="0.06*(sin(2*PI*7040*t)+sin(2*PI*9317*t)+sin(2*PI*11777*t))*exp(-60*$OFFBEAT)"
readonly CHORD="0.08*($(saw 220)+$(saw 261.63)+$(saw 329.63))"
readonly LOOP="$KICK+$HAT+$CHORD"

out="${1:?usage: render-backspin.sh <out.wav>}"
mkdir -p "$(dirname "$out")"
out="$(cd "$(dirname "$out")" && pwd)/$(basename "$out")"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

ffmpeg -y -loglevel error -f lavfi -i "aevalsrc=exprs='$LOOP|$LOOP':s=$SOURCE_RATE:d=$SOURCE_SECONDS" \
    -c:a pcm_s16le "$work/source.wav"
sed "s#$SOURCE_PLACEHOLDER#$work/source.wav#" "$SCENARIO_TEMPLATE" > "$work/backspin.json"

dotnet run --project "$REPO_ROOT/tools/Sholto.Interface.Bench" -- \
    headless --scenario "$work/backspin.json" --out "$out" > "$work/bench.json"

frames="$(ffprobe -v error -select_streams a:0 -show_entries stream=duration_ts -of csv=p=0 "$out")"
echo "wav: $out"
echo "frames: $frames"
