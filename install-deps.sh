#!/bin/bash
# Sholto — install ONLY the runtime tools the app needs, for people running a
# prebuilt binary from the Releases page. No .NET SDK, no build, no repo clone.
#   (Building from source? Use install.sh instead — it does all of this plus the
#    SDK and the build.)
# Idempotent: safe to re-run.  Ubuntu / Mint / Pop!_OS / Debian.
set -e

if [ -t 1 ]; then
    BOLD=$'\033[1m'; DIM=$'\033[2m'; GREEN=$'\033[1;32m'; CYAN=$'\033[1;36m'; BLUE=$'\033[1;34m'; RESET=$'\033[0m'
else
    BOLD=''; DIM=''; GREEN=''; CYAN=''; BLUE=''; RESET=''
fi
ok()   { echo "  ${GREEN}✓${RESET} $*"; }
info() { echo "  ${CYAN}·${RESET} ${DIM}$*${RESET}"; }

echo ""
echo "${BOLD}${BLUE}Sholto${RESET} — ${DIM}runtime dependencies${RESET}"

# 1. System libraries + ffmpeg. NOT the .NET SDK — the release binary is
#    self-contained. libfontconfig1/libglib2.0-0 are needed by Skia/Avalonia,
#    libpulse0 by the audio engine.
sudo apt-get update -q
sudo apt-get install -y ffmpeg libpulse0 libfontconfig1 libglib2.0-0 curl wget
ok "ffmpeg + runtime libraries"

# 2. libpulse.so symlink — miniaudio (under SoundFlow) dlopens the unversioned
#    name, but most distros ship only libpulse.so.0.
LIB="/usr/lib/x86_64-linux-gnu"
if [ ! -f "$LIB/libpulse.so" ]; then sudo ln -sf libpulse.so.0 "$LIB/libpulse.so"; fi
ok "libpulse.so"

# 3. uv — installs the Python analysis tools in their own isolated environments
#    without touching the system Python.
if ! command -v uv &>/dev/null; then
    info "Installing uv (Python tool manager)..."
    curl -LsSf https://astral.sh/uv/install.sh | sh
    export PATH="$HOME/.local/bin:$PATH"
fi
ok "uv $(uv --version 2>/dev/null | awk '{print $2}')"

# 4. madmom — REQUIRED. Beat/tempo detection; without a beatgrid a track won't play.
uv tool install madmom-onnx && ok "madmom-onnx (beat tracker)"

# 5. demucs — OPTIONAL. Stem separation (drums / vocals / bass / other).
uv tool install demucs && ok "demucs (stems)" || info "demucs failed — stems disabled"

# 6. allin1 — OPTIONAL, large (PyTorch). AI song sections. Off unless asked for.
if [ "${SHOLTO_INSTALL_ALLIN1:-0}" = "1" ]; then
    uv tool install "allin1" --with "torch" --with "natten" \
        --with "madmom @ git+https://github.com/CPJKU/madmom" \
        && ok "allin1 (AI song sections)" \
        || info "allin1 failed — Sholto falls back to the built-in segmenter"
else
    info "Skipping allin1 (set SHOLTO_INSTALL_ALLIN1=1 to enable AI song sections)"
fi

echo ""
ok "Done. Ensure ~/.local/bin is on your PATH, then run ./Sholto.App"
