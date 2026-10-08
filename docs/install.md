# Installing Sholto

Sholto runs on **64-bit Linux** — the installers support **Ubuntu, Mint, Pop!_OS
and Debian**. Windows, macOS, and more controllers are on the way.

Three ways to get it. Pick one.

## Option 1 — one command (easiest)

```bash
curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash
```

That downloads the latest release into `~/sholto`, installs the runtime tools it
needs (ffmpeg, madmom, and a couple of system libraries — it will ask for your
password once), and launches Sholto. Run the same command again later to update.
Set `SHOLTO_DIR=/somewhere` to install elsewhere, or add `--no-run` to install
without launching:

```bash
curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash -s -- --no-run
```

Prefer to see what you're running first? Download the script, read it, then
`bash get-sholto.sh`.

## Option 2 — download the release yourself

The binary is **self-contained — you don't need .NET installed.**

1. Download `sholto-vX.Y.Z-linux-x64.tar.gz` from the
   [**latest release**](https://github.com/freedomfirst26/Sholto/releases/latest).
2. Unpack it into a folder of its own — the archive has no top-level folder, so
   make one:

   ```bash
   mkdir -p ~/sholto
   tar -xzf ~/Downloads/sholto-*-linux-x64.tar.gz -C ~/sholto
   ```

3. Install the runtime tools once (asks for your password):

   ```bash
   cd ~/sholto
   bash install-deps.sh
   ```

4. Run it:

   ```bash
   ./Sholto
   ```

The archive holds the `Sholto` program, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `docs/license.md`, `install-deps.sh`,
`sholto-deps.sh` (the tool versions and checks `install-deps.sh` uses) and
`get-sholto.sh`.

## Option 3 — build from source

```bash
git clone https://github.com/freedomfirst26/Sholto.git
cd Sholto
bash install.sh
dotnet run -c Release --project src/Sholto.Interface.MainUI
```

`install.sh` sets up everything Sholto needs — including .NET and all the tools
below — and is safe to re-run. It works on modern Ubuntu, Mint, Pop!_OS, and
Debian.

## First run

On startup Sholto scans your music folder (change it in **Settings → Music
folder…**). Highlight a track and press **1** or **2** on the keyboard, or
**LOAD 1** / **LOAD 2** on the controller, to load it onto a deck. See
[the guide](README.md) for everything on screen.

**Your controller** — a **Pioneer DDJ-FLX4** is picked up automatically when you
plug it in; a green dot in the top bar means it's connected (red means reconnect
the USB), and it reconnects on its own if it drops. You don't need one —
everything works from the mouse and keyboard.

## What it needs

`install.sh` (and `install-deps.sh` for the prebuilt binary) installs all of this
for you on Ubuntu / Mint / Pop!_OS / Debian. On another distro, install the
equivalents:

**Required**
- **.NET 10** — to build and run Sholto from source. The prebuilt binary does
  not need it.
- **ffmpeg** — decodes your audio for the beat detector, and also decodes
  M4A/AAC files for playback.
- **madmom** (the `madmom-onnx` build) — finds the beats and tempo. Sholto can't
  play a track until it has a beatgrid, so this one isn't optional.
- A normal **Linux desktop** (X11 or Wayland) and a working **sound system**
  (PipeWire, PulseAudio, or ALSA).

**Optional** — Sholto runs fine without these, you just lose that one feature:
- **demucs** — stem separation (drums / vocals / bass / other). Without it, the
  stem chips and the hot-cue stem mutes are unavailable.

**Checking they actually work.** The background tools are ordinary Python
programs, and an update to one of them can leave it installed but broken. Run
`bash install.sh --verify` (or `bash install-deps.sh --verify` if you're on the
prebuilt binary) and Sholto will run each one for real — it separates a test clip
and confirms it gets four stems back, rather than just checking the program is
there. It tells you which one is broken and what you lose without it.

## What Sholto is built on

Sholto doesn't try to invent beat detection or stem separation from scratch —
those are hard research problems, and there are people who have spent careers on
them. It stands on their work and concentrates on being a good instrument to play.

Two very different kinds of borrowing are going on here, and the difference
matters.

**Programs Sholto runs** — separate applications that Sholto starts, hands a file
to, and waits for. They are installed alongside Sholto rather than inside it. If
one is missing or broken, Sholto keeps working and you lose only that feature.

| Program | What it does for you | Without it |
|---|---|---|
| **madmom** (`madmom-onnx`) | Listens to a track and works out where every beat and every bar starts. This is the foundation everything else stands on — sync, looping, the grid on the waveform, quantised cues. | **Required.** A track can't play without a beatgrid. |
| **demucs** | Splits a finished track back into four separate recordings — drums, bass, vocals, everything else — so you can drop the vocal out of one track while keeping its drums. | Stem chips and hot-cue stem mutes are unavailable. |
| **ffmpeg** | The universal audio translator. Converts M4A/AAC files into something Sholto can play, and prepares audio for the beat detector. | M4A/AAC files won't load. |

**Libraries built into Sholto** — code compiled into the app itself. You never
install these separately; they arrive with it.

| Library | What it does for you |
|---|---|
| **Avalonia** | Draws the entire interface — decks, waveforms, library, every control. It's what lets Sholto look the same on any Linux desktop. |
| **SoundFlow** | Pushes audio out to your speakers, and is what makes the pitch and tempo changes sound right rather than chipmunky. |
| **NAudio** + **NLayer** | Read WAV, FLAC and MP3 files and turn them into the samples the decks play. |
| **z440.atl.core** | Reads the artist, title, album and artwork embedded in your files, so the library list isn't just filenames. |
| **SQLite** + **Entity Framework Core** | Remember everything between sessions — your analysed tracks, cue points, crates, tags and ratings. |

**A note for anyone licensing Sholto commercially.** The programs in the first
table are deliberately kept at arm's length — Sholto launches them as separate
processes and talks to them through files and command output. None of their code
is linked into Sholto. That boundary is intentional, because those programs carry
their own licences that differ from Sholto's. See
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) — in particular, madmom's
trained models are non-commercial, and a commercial licence to Sholto does not
carry a right to use them.
