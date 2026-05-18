#!/bin/bash
# CommunityDj — one-shot install script for Linux (Ubuntu / Mint / Pop! / Debian).
# Idempotent: safe to re-run.
set -e

ok()   { echo "  ✓ $*"; }
info() { echo "  · $*"; }

echo ""
echo "CommunityDj — install"
echo "====================="

# ── 1. System packages ────────────────────────────────────────────────────────
echo ""
echo "── system packages ────────────────────────────────────────────────────────"
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
echo ""
echo "── libpulse.so symlink ───────────────────────────────────────────────────"
LIB="/usr/lib/x86_64-linux-gnu"
if [ -f "$LIB/libpulse.so" ]; then
    ok "$LIB/libpulse.so already present"
else
    sudo ln -sf libpulse.so.0 "$LIB/libpulse.so"
    ok "linked $LIB/libpulse.so → libpulse.so.0"
fi

# ── 3. madmom (beat tracker) ──────────────────────────────────────────────────
echo ""
echo "── madmom (beat tracker) ─────────────────────────────────────────────────"
if ! command -v uv &>/dev/null; then
    info "Installing uv (Python tool manager)..."
    curl -LsSf https://astral.sh/uv/install.sh | sh
    export PATH="$HOME/.local/bin:$PATH"
fi
ok "uv $(uv --version | awk '{print $2}')"

if [ -x "$HOME/.local/bin/DBNDownBeatTracker" ]; then
    ok "madmom-onnx already installed"
else
    info "Installing madmom-onnx (ONNX-runtime fork that builds on Python 3.12+)..."
    uv tool install madmom-onnx
    ok "madmom-onnx installed at ~/.local/bin/"
fi

# ── 4. NuGet restore ──────────────────────────────────────────────────────────
echo ""
echo "── NuGet packages ────────────────────────────────────────────────────────"
dotnet restore CommunityDj.slnx
ok "restored"

echo ""
echo "Done.  Run with:  dotnet run --project src/CommunityDj.App"
echo ""
