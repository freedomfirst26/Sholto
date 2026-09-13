# Sholto — shared definition of the Python analysis tools.
#
# SOURCED, not executed. Both install.sh (build from source) and install-deps.sh
# (prebuilt binary from the Releases page) source this file, so the pins and the
# health checks exist in exactly one place. It is shipped inside the release
# tarball alongside install-deps.sh — see .github/workflows/release.yml.
#
# ─────────────────────────────────────────────────────────────────────────────
# THE KNOWN-GOOD SET. Verified end to end on 2026-09-10: a real track separated
# to four non-empty stems, DBNDownBeatTracker runs.
#
#   demucs      : demucs 4.1.0 · torch 2.14.0 · torchcodec 0.16.0 · numpy 2.5.3
#   madmom-onnx : madmom-onnx 0.17.dev0 · numpy 2.4.5 · onnxruntime 1.26.0
#                 · scipy 1.17.1
#   Python      : 3.12  (uv fetches a managed 3.12 if the system hasn't got one)
#
# Why pin at all: on 2026-09-05 an unpinned `uv tool install demucs` resolved
# torchaudio 2.9, which had dropped its audio-writing backend. Demucs separated
# every track correctly and then failed at the final save — empty output dir, no
# error the app could see. Five days of analysed tracks silently got no stems.
#
# What is NOT pinned, on purpose:
#   · the nvidia/CUDA wheels torch drags in — machine- and driver-specific;
#     pinning them turns a degraded install into a failed one
#   · ~45 transitive packages of the demucs environment (huggingface-hub,
#     einops, julius, lameenc, …). These pins narrow the blast radius; they do
#     not make the install bit-for-bit reproducible. If that is ever needed,
#     generate a lockfile per tool with `uv pip compile --generate-hashes` and
#     feed it to `uv tool install --with-requirements`.
#
# To bump: edit the versions here — nowhere else — run the installer, then run
# it again with --verify. Keep the bump only if verify reports four stems.
# ─────────────────────────────────────────────────────────────────────────────

SHOLTO_PYTHON="3.12"

SHOLTO_MADMOM_SPEC="madmom-onnx==0.17.dev0"
SHOLTO_MADMOM_WITH=(--with numpy==2.4.5 --with onnxruntime==1.26.0 --with scipy==1.17.1)

# ⚠ torchaudio is ABSENT FROM THIS LIST ON PURPOSE — DO NOT ADD IT BACK.
# demucs 4.1.0 writes its stems through torchcodec, and the known-good
# environment has no torchaudio installed at all. Re-adding it is what caused
# the Sep 2026 outage: torchaudio 2.9's save() became a thin wrapper over
# torchcodec, so demucs picked torchaudio's broken path instead of the working
# one. If a future demucs genuinely needs it, prove it with --verify first.
SHOLTO_DEMUCS_SPEC="demucs==4.1.0"
SHOLTO_DEMUCS_WITH=(--with torch==2.14.0 --with torchcodec==0.16.0 --with numpy==2.5.3)

# ── Binary resolution ────────────────────────────────────────────────────────
# Resolve each tool exactly the way the app resolves it, so a check can never
# pass for a binary the app will not run.
#
#   demucs           — DemucsStemAnalyzer.cs:98 starts the bare name "demucs",
#                      so the OS resolves it from PATH. `command -v` matches.
#   DBNDownBeatTracker
#                    — MadmomBeatAnalyzer.cs:98 FindBinary() tries
#                      ~/.local/bin, /usr/local/bin, /usr/bin, then PATH.

# Print the path the app would run for a tool resolved via PATH, or fail.
deps_which() { command -v "$1" 2>/dev/null; }

# Print the path the app's FindBinary() would pick, or fail.
deps_find_binary() {
    local name=$1 c
    for c in "$HOME/.local/bin/$name" "/usr/local/bin/$name" "/usr/bin/$name"; do
        [ -f "$c" ] && { printf '%s\n' "$c"; return 0; }
    done
    deps_which "$name"
}

# ── Functional checks ────────────────────────────────────────────────────────
# A file existing at ~/.local/bin doesn't prove the tool works — that assumption
# is precisely how the Sep 2026 demucs breakage stayed invisible for five days.
# These run each tool for real.
#
# Each check sets SHOLTO_CHECK_DETAIL to a short line naming the binary it
# actually tested (and any problem), so the installer can print which one it
# was — a conda or pipx build earlier on PATH is otherwise invisible.

SHOLTO_CHECK_DETAIL=""

check_madmom() {
    local bin
    SHOLTO_CHECK_DETAIL=""
    bin=$(deps_find_binary DBNDownBeatTracker) || { SHOLTO_CHECK_DETAIL="not found"; return 1; }
    SHOLTO_CHECK_DETAIL="$bin"
    [ -x "$bin" ] && "$bin" -h >/dev/null 2>&1
}

# Separate one second of silence and assert the four files the app will look
# for. Deliberately mirrors DemucsStemAnalyzer.AnalyzeAsync: same --out /
# --filename arguments, same expected <out>/htdemucs/<stem>.wav layout. Counting
# "some wavs somewhere" would pass for a model that writes a differently named
# directory, which the app would then fail to find — the same silent failure in
# a new costume.
check_demucs() {
    local bin tmp missing=""
    SHOLTO_CHECK_DETAIL=""

    bin=$(deps_which demucs) || { SHOLTO_CHECK_DETAIL="not found on PATH"; return 1; }
    SHOLTO_CHECK_DETAIL="$bin"

    if ! command -v ffmpeg >/dev/null 2>&1; then
        SHOLTO_CHECK_DETAIL="$bin (cannot test: ffmpeg is not installed)"
        return 1
    fi

    # Cheap pre-check: does the binary even start? Catches a broken/missing
    # Python environment in well under a second, before paying for the full
    # model-load-and-separate check below.
    if ! "$bin" --help >/dev/null 2>&1; then
        SHOLTO_CHECK_DETAIL="$bin (does not run — broken install?)"
        return 1
    fi

    # This is the gate every installer runs before deciding whether to (re)install,
    # and it is a REAL separation — model load + inference — so it can take tens of
    # seconds with no other output. Say so up front rather than leaving the terminal
    # looking frozen.
    info "Verifying demucs with a real test separation (loads the model — can take tens of seconds)..."

    tmp=$(mktemp -d) || return 1
    # Clean up the temp dir even if we're interrupted mid-separation.
    trap 'rm -rf "$tmp"' EXIT INT TERM

    if ! ffmpeg -f lavfi -i anullsrc=r=44100:cl=stereo -t 1 -y "$tmp/silence.wav" -loglevel error >/dev/null 2>&1 \
        || [ ! -s "$tmp/silence.wav" ]; then
        SHOLTO_CHECK_DETAIL="$bin (could not generate test audio — ffmpeg failed, not demucs)"
        rm -rf "$tmp"; trap - EXIT INT TERM
        return 1
    fi

    "$bin" --out "$tmp/out" --filename "{stem}.{ext}" "$tmp/silence.wav" >/dev/null 2>&1

    local stem
    for stem in vocals drums bass other; do
        [ -s "$tmp/out/htdemucs/$stem.wav" ] || missing="$missing $stem"
    done
    rm -rf "$tmp"
    trap - EXIT INT TERM

    if [ -n "$missing" ]; then
        SHOLTO_CHECK_DETAIL="$bin (no output for:$missing)"
        return 1
    fi

    # A demucs that works but isn't the one we pinned is worth saying out loud:
    # it is the most likely explanation for a working check and a broken app.
    if [ "$bin" != "$HOME/.local/bin/demucs" ]; then
        SHOLTO_CHECK_DETAIL="$bin — NOTE: not the uv-managed install at ~/.local/bin/demucs"
    fi
    return 0
}

# ── Shared --verify entry point ──────────────────────────────────────────────
# Exits non-zero if a tool the app depends on is broken, so this can be used in
# a health check or CI.
#
# Callers must have defined ok/info/warn before sourcing or calling this.
deps_verify() {
    local rc=0

    if check_madmom; then ok "madmom-onnx working  ${DIM}(${SHOLTO_CHECK_DETAIL})${RESET}"
    else warn "madmom-onnx NOT working  (${SHOLTO_CHECK_DETAIL:-unknown}) — tracks will not get a beatgrid"; rc=1; fi

    if check_demucs; then ok "demucs working  ${DIM}(${SHOLTO_CHECK_DETAIL})${RESET} — test separation produced 4 stems"
    else warn "demucs NOT working  (${SHOLTO_CHECK_DETAIL:-unknown}) — stems will be missing"; rc=1; fi

    return $rc
}
