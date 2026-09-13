#!/bin/bash
# Sholto — install ONLY the runtime tools the app needs, for people running a
# prebuilt binary from the Releases page. No .NET SDK, no build, no repo clone.
#   (Building from source? Use install.sh instead — it does all of this plus the
#    SDK and the build.)
# Idempotent: safe to re-run.  Ubuntu / Mint / Pop!_OS / Debian.
set -e

if [ -t 1 ]; then
    BOLD=$'\033[1m'; DIM=$'\033[2m'; GREEN=$'\033[1;32m'; CYAN=$'\033[1;36m'; YELLOW=$'\033[1;33m'; BLUE=$'\033[1;34m'; RESET=$'\033[0m'
else
    BOLD=''; DIM=''; GREEN=''; CYAN=''; YELLOW=''; BLUE=''; RESET=''
fi
ok()   { echo "  ${GREEN}✓${RESET} $*"; }
info() { echo "  ${CYAN}·${RESET} ${DIM}$*${RESET}"; }
warn() { echo "  ${YELLOW}✗${RESET} $*" >&2; }

# ── Shared tool definitions ───────────────────────────────────────────────────
# Pins and health checks live in sholto-deps.sh so install.sh and this script
# cannot drift apart. It ships in the release tarball next to this file.
DEPS_DIR="$(dirname "$(readlink -f "$0")")"
if [ ! -r "$DEPS_DIR/sholto-deps.sh" ]; then
    warn "sholto-deps.sh is missing from $DEPS_DIR — re-extract the release tarball."
    exit 1
fi
# shellcheck source=sholto-deps.sh
. "$DEPS_DIR/sholto-deps.sh"

if [ "${1:-}" = "--verify" ]; then
    echo ""
    echo "${BOLD}${BLUE}Sholto${RESET} — ${DIM}verify${RESET}"
    if deps_verify; then exit 0; else exit 1; fi
fi

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
#    Versions and the reasoning behind them are in sholto-deps.sh.
# Unlike demucs below, a broken madmom is fatal to the install — see the
# check at the end of this script. We still `|| true` the install command itself
# (a failed `uv tool install` shouldn't kill the script under set -e before we
# get a chance to print a proper error) and keep going so demucs below
# still gets installed for a user who wants to fix madmom afterwards.
MADMOM_BROKEN=0
if check_madmom; then
    ok "madmom-onnx (beat tracker) already working"
else
    uv tool install --force --python "$SHOLTO_PYTHON" \
        "$SHOLTO_MADMOM_SPEC" "${SHOLTO_MADMOM_WITH[@]}" || true
    if check_madmom; then
        ok "madmom-onnx (beat tracker)"
    else
        warn "madmom-onnx installed but failed its functional check (${SHOLTO_CHECK_DETAIL})"
        MADMOM_BROKEN=1
    fi
fi

# 5. demucs — OPTIONAL. Stem separation (drums / vocals / bass / other).
if check_demucs; then
    ok "demucs (stems) already working"
else
    uv tool install --force --python "$SHOLTO_PYTHON" \
        "$SHOLTO_DEMUCS_SPEC" "${SHOLTO_DEMUCS_WITH[@]}" || true
    if check_demucs; then
        ok "demucs (stems) — verified with a test separation"
    else
        info "demucs installed but failed its functional check (${SHOLTO_CHECK_DETAIL}) — stems disabled"
    fi
fi

echo ""
if [ "$MADMOM_BROKEN" = "1" ]; then
    warn "madmom-onnx (beat tracker) is REQUIRED and still not working — Sholto will run"
    warn "but no track will get a beatgrid, so nothing will play."
    warn "Check the warning above, fix your network/Python environment, then re-run"
    warn "./install-deps.sh (or just './install-deps.sh --verify' to recheck)."
    exit 1
fi
ok "Done. Ensure ~/.local/bin is on your PATH, then run ./Sholto.App"
