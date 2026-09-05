#!/usr/bin/env bash
# Sholto — download the latest release, install its runtime tools, and run it.
#
#   curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash
#
# Installs into ~/sholto (override with SHOLTO_DIR=/path). Re-run any time to
# update to the newest release. Pass --no-run to install without launching.
set -euo pipefail

REPO="freedomfirst26/Sholto"
DIR="${SHOLTO_DIR:-$HOME/sholto}"
RUN=1
[ "${1:-}" = "--no-run" ] && RUN=0

need() { command -v "$1" >/dev/null 2>&1 || { echo "error: '$1' is required" >&2; exit 1; }; }
need curl; need tar

echo "Sholto — install"
echo "  finding the latest release..."
API=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest")
TAG=$(printf '%s' "$API" | sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p' | head -1)
URL=$(printf '%s' "$API" | sed -n 's/.*"browser_download_url": *"\([^"]*linux-x64\.tar\.gz\)".*/\1/p' | head -1)
[ -n "$TAG" ] && [ -n "$URL" ] || { echo "error: could not find a linux-x64 release on GitHub" >&2; exit 1; }

if [ -f "$DIR/.version" ] && [ "$(cat "$DIR/.version")" = "$TAG" ]; then
  echo "  $TAG is already installed in $DIR"
else
  echo "  downloading $TAG into $DIR"
  mkdir -p "$DIR"
  TMP=$(mktemp)
  curl -fL --progress-bar -o "$TMP" "$URL"
  tar -xzf "$TMP" -C "$DIR"
  rm -f "$TMP"
  chmod +x "$DIR/Sholto.App"
  printf '%s\n' "$TAG" > "$DIR/.version"
  echo "  installing runtime tools (ffmpeg, madmom, libraries) — may ask for your password"
  # When run as `curl ... | bash`, stdin is the script itself, so sudo could not
  # read a password. Hand the installer the real terminal when there is one.
  if [ -r /dev/tty ]; then bash "$DIR/install-deps.sh" < /dev/tty; else bash "$DIR/install-deps.sh"; fi
fi

echo "  done: $DIR/Sholto.App"
if [ "$RUN" = 1 ]; then
  echo "  launching..."
  cd "$DIR"
  exec ./Sholto.App
fi
