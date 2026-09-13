#!/bin/bash
# Sholto — one-shot install script for Linux (Ubuntu / Mint / Pop! / Debian).
# Idempotent: safe to re-run.
set -e

# Run from the repo root no matter where the script was invoked from, so the
# dotnet restore step finds Sholto.slnx.
cd "$(dirname "$(readlink -f "$0")")"

# ANSI colours — auto-disabled when stdout isn't a TTY (e.g. piped to a file).
if [ -t 1 ]; then
    BOLD=$'\033[1m'; DIM=$'\033[2m'
    GREEN=$'\033[1;32m'; CYAN=$'\033[1;36m'; YELLOW=$'\033[1;33m'
    MAGENTA=$'\033[1;35m'; BLUE=$'\033[1;34m'; RESET=$'\033[0m'
else
    BOLD=''; DIM=''; GREEN=''; CYAN=''; YELLOW=''; MAGENTA=''; BLUE=''; RESET=''
fi

ok()      { echo "  ${GREEN}✓${RESET} $*"; }
info()    { echo "  ${CYAN}·${RESET} ${DIM}$*${RESET}"; }
warn()    { echo "  ${YELLOW}✗${RESET} $*" >&2; }
section() { echo ""; echo "${MAGENTA}── $* ──────────────────────────────────────────────────${RESET}"; }

# ── Shared tool definitions ───────────────────────────────────────────────────
# Pins and health checks live in sholto-deps.sh so this script and
# install-deps.sh cannot drift apart. We have already cd'd to the repo root.
if [ ! -r ./sholto-deps.sh ]; then
    warn "sholto-deps.sh is missing from $(pwd) — check out the full repo (this script must run from a Sholto git checkout, not a standalone copy)."
    exit 1
fi
# shellcheck source=sholto-deps.sh
. ./sholto-deps.sh

if [ "${1:-}" = "--verify" ]; then
    section "verify"
    if deps_verify; then exit 0; else exit 1; fi
fi

echo ""
echo "${BOLD}${BLUE}Sholto${RESET} — ${DIM}install${RESET}"
echo "${DIM}=====================${RESET}"

# ── 1. System packages ────────────────────────────────────────────────────────
section "system packages"
sudo apt-get update -q

# .NET 10 sometimes isn't in the default repos. Add the Microsoft feed first.
if ! apt-cache show dotnet-sdk-10.0 >/dev/null 2>&1; then
    info "Adding Microsoft package feed for .NET 10..."
    UBUNTU_VERSION=$(grep VERSION_ID /etc/os-release | cut -d'"' -f2 | head -1)
    wget -q "https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION}/packages-microsoft-prod.deb" -O /tmp/ms.deb
    sudo dpkg -i /tmp/ms.deb && rm /tmp/ms.deb
    sudo apt-get update -q
fi

sudo apt-get install -y \
    dotnet-sdk-10.0 \
    ffmpeg \
    libpulse0 \
    libfontconfig1 \
    libglib2.0-0 \
    curl wget
ok ".NET $(dotnet --version)"
ok "ffmpeg $(ffmpeg -version | head -1 | awk '{print $3}')"

# ── 2. libpulse.so symlink ────────────────────────────────────────────────────
# miniaudio (under SoundFlow) dlopens "libpulse.so" first. Most distros ship
# only the versioned libpulse.so.0, so we add the unversioned symlink.
section "libpulse.so symlink"
LIB="/usr/lib/x86_64-linux-gnu"
if [ -f "$LIB/libpulse.so" ]; then
    ok "$LIB/libpulse.so already present"
else
    sudo ln -sf libpulse.so.0 "$LIB/libpulse.so"
    ok "linked $LIB/libpulse.so → libpulse.so.0"
fi

# The pinned versions of the Python analysis tools, and the full explanation of
# why each pin exists and how to bump it, are in sholto-deps.sh — sourced above.

# ── 3. madmom (beat tracker) ──────────────────────────────────────────────────
section "madmom (beat tracker)"
if ! command -v uv &>/dev/null; then
    info "Installing uv (Python tool manager)..."
    curl -LsSf https://astral.sh/uv/install.sh | sh
    export PATH="$HOME/.local/bin:$PATH"
fi
ok "uv $(uv --version | awk '{print $2}')"

# madmom is REQUIRED — unlike demucs below, a broken madmom fails the
# whole install (see the check just before the final "Done" banner). We still
# `|| true` the install command itself so a failed `uv tool install` doesn't
# kill the script under set -e before we get to print a proper error, and we
# keep going rather than exit here: a user with a broken madmom may still want
# Sholto built so they can retry the tool afterwards without rebuilding too.
MADMOM_BROKEN=0
if check_madmom; then
    ok "madmom-onnx already installed and working"
else
    info "Installing madmom-onnx (ONNX-runtime fork that builds on Python 3.12+)..."
    uv tool install --force --python "$SHOLTO_PYTHON" \
        "$SHOLTO_MADMOM_SPEC" "${SHOLTO_MADMOM_WITH[@]}" || true
    if check_madmom; then
        ok "madmom-onnx installed at ~/.local/bin/"
    else
        warn "madmom-onnx installed but failed its functional check (${SHOLTO_CHECK_DETAIL})"
        MADMOM_BROKEN=1
    fi
fi

# ── 4. demucs (stem separation) ───────────────────────────────────────────────
section "demucs (stem separation)"
if check_demucs; then
    ok "demucs already installed and working"
else
    info "Installing demucs (htdemucs 4-stem source separation)..."
    uv tool install --force --python "$SHOLTO_PYTHON" \
        "$SHOLTO_DEMUCS_SPEC" "${SHOLTO_DEMUCS_WITH[@]}" || true
    if check_demucs; then
        ok "demucs installed at ~/.local/bin/ — verified with a test separation"
    else
        warn "demucs installed but failed its functional check (${SHOLTO_CHECK_DETAIL}) — stems will not be produced"
    fi
fi

# ── 5. NuGet restore ──────────────────────────────────────────────────────────
section "NuGet packages"
dotnet restore Sholto.slnx
ok "restored"

# ── 6. Build release binary ───────────────────────────────────────────────────
# Self-contained single-file publish: bundles the .NET runtime so end users
# don't need the SDK on PATH. Output goes to ./dist/linux-x64/Sholto.App.
section "release build"
DIST="$PWD/dist/linux-x64"
dotnet publish src/Sholto.App/Sholto.App.csproj \
    -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$DIST" >/dev/null
ok "published → $DIST/Sholto.App"

echo ""
if [ "$MADMOM_BROKEN" = "1" ]; then
    echo "${BOLD}${YELLOW}Built, but not ready.${RESET}"
    echo "  ${DIM}Binary:${RESET} ${CYAN}$DIST/Sholto.App${RESET} ${DIM}(built successfully)${RESET}"
    warn "madmom-onnx (beat tracker) is REQUIRED and still not working — no track will"
    warn "get a beatgrid, so nothing will play in the app you just built."
    warn "Check the warning above, fix your network/Python environment, then re-run"
    warn "./install.sh (or just './install.sh --verify' to recheck) before using Sholto."
    echo ""
    exit 1
fi
echo "${BOLD}${GREEN}Done.${RESET}"
echo "  ${DIM}Binary:${RESET} ${CYAN}$DIST/Sholto.App${RESET}"
echo ""
