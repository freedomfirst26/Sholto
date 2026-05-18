#!/bin/bash
# CommunityDj dev environment setup
# Run once on a new machine: bash dev.sh
# Safe to re-run — all steps are idempotent.
set -e

DOTNET_VERSION="10.0"

# ── Helpers ────────────────────────────────────────────────────────────────────

ok()   { echo "  ✓ $*"; }
info() { echo "  · $*"; }
fail() { echo "  ✗ $*" >&2; exit 1; }

need_cmd() { command -v "$1" &>/dev/null || fail "Required command not found: $1 (install it and retry)"; }

# ── Distro detection ───────────────────────────────────────────────────────────

detect_ubuntu_codename() {
    if [ -f /etc/os-release ]; then
        # Mint and other Ubuntu derivatives expose UBUNTU_CODENAME
        local codename
        codename=$(grep UBUNTU_CODENAME /etc/os-release | cut -d= -f2)
        if [ -z "$codename" ]; then
            codename=$(grep VERSION_CODENAME /etc/os-release | cut -d= -f2)
        fi
        echo "$codename"
    fi
}

detect_ubuntu_version() {
    # Map codename to version number for Microsoft feed URL
    local codename="$1"
    case "$codename" in
        noble)   echo "24.04" ;;
        jammy)   echo "22.04" ;;
        focal)   echo "20.04" ;;
        bookworm|xia) echo "24.04" ;;  # Mint 22 / Debian 12 → use 24.04 feed
        *)       echo "24.04" ;;        # fallback
    esac
}

# ── .NET SDK ───────────────────────────────────────────────────────────────────

install_dotnet() {
    echo ""
    echo "── .NET SDK ──────────────────────────────────────────────────────────────"

    if dotnet --version 2>/dev/null | grep -q "^${DOTNET_VERSION%%.*}\."; then
        ok ".NET $(dotnet --version) already installed"
        return
    fi

    need_cmd wget
    need_cmd dpkg

    local codename
    codename=$(detect_ubuntu_codename)
    local ubuntu_version
    ubuntu_version=$(detect_ubuntu_version "$codename")
    info "Detected Ubuntu-compatible base: $ubuntu_version (codename: $codename)"

    local feed_deb="packages-microsoft-prod.deb"
    local feed_url="https://packages.microsoft.com/config/ubuntu/${ubuntu_version}/${feed_deb}"

    if ! dpkg -l packages-microsoft-prod &>/dev/null; then
        info "Adding Microsoft package feed..."
        wget -q "$feed_url" -O "/tmp/${feed_deb}"
        sudo dpkg -i "/tmp/${feed_deb}"
        rm "/tmp/${feed_deb}"
        sudo apt-get update -q
        ok "Microsoft package feed added"
    else
        ok "Microsoft package feed already present"
    fi

    info "Installing dotnet-sdk-${DOTNET_VERSION%%.*}.0..."
    sudo apt-get install -y "dotnet-sdk-${DOTNET_VERSION%%.*}.0"
    ok ".NET $(dotnet --version) installed"
}

# ── System libraries ───────────────────────────────────────────────────────────

install_system_libs() {
    echo ""
    echo "── System libraries ──────────────────────────────────────────────────────"

    local packages=(
        libportaudio2       # PortAudio runtime  — audio output to DDJ-FLX4 USB interface
        portaudio19-dev     # PortAudio headers  — needed by PortAudioSharp NuGet native compile
        libasound2-dev      # ALSA headers       — needed by RtMidi.Core for MIDI on Linux
        libglib2.0-0        # GLib runtime       — Avalonia dependency on Linux
        libfontconfig1      # Font system        — Avalonia text rendering
    )

    local to_install=()
    for pkg in "${packages[@]}"; do
        if dpkg -l "$pkg" 2>/dev/null | grep -q "^ii"; then
            ok "$pkg already installed"
        else
            to_install+=("$pkg")
        fi
    done

    if [ ${#to_install[@]} -gt 0 ]; then
        info "Installing: ${to_install[*]}"
        sudo apt-get install -y "${to_install[@]}"
        ok "All system libraries installed"
    fi
}

# ── just (command runner) ──────────────────────────────────────────────────────

install_just() {
    echo ""
    echo "── just (command runner) ─────────────────────────────────────────────────"

    if command -v just &>/dev/null; then
        ok "just $(just --version) already installed"
        return
    fi

    info "Installing just..."
    # Use the official installer — works on all Linux distros
    curl -sSfL https://just.systems/install.sh | sudo bash -s -- --to /usr/local/bin
    ok "just $(just --version) installed"
}

# ── NuGet restore ──────────────────────────────────────────────────────────────

restore_packages() {
    echo ""
    echo "── NuGet packages ────────────────────────────────────────────────────────"

    local sln
    sln=$(find "$(dirname "$0")" -maxdepth 1 \( -name "*.slnx" -o -name "*.sln" \) | head -1)

    if [ -z "$sln" ]; then
        info "No solution file found yet — skipping restore (run after Task 2 scaffold)"
        return
    fi

    info "Restoring NuGet packages for $(basename "$sln")..."
    dotnet restore "$sln"
    ok "NuGet packages restored"
}

# ── Summary ────────────────────────────────────────────────────────────────────

print_summary() {
    echo ""
    echo "── Summary ───────────────────────────────────────────────────────────────"
    echo "  .NET:        $(dotnet --version)"
    echo "  just:        $(just --version 2>/dev/null || echo 'not found')"
    echo "  PortAudio:   $(dpkg -l libportaudio2 2>/dev/null | grep ^ii | awk '{print $3}' || echo 'not found')"
    echo "  ALSA dev:    $(dpkg -l libasound2-dev 2>/dev/null | grep ^ii | awk '{print $3}' || echo 'not found')"
    echo ""
    echo "  Dev environment ready. Run: just run"
    echo ""
}

# ── Main ───────────────────────────────────────────────────────────────────────

echo ""
echo "CommunityDj — dev environment setup"
echo "================================================="

install_dotnet
install_system_libs
install_just
restore_packages
print_summary
