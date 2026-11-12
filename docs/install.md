# Installing Sholto

Sholto runs on **64-bit Linux** and **64-bit Windows 10 or 11**. On Linux the
installers support **Ubuntu, Mint, Pop!_OS and Debian**. macOS and more controllers
are on the way.

- [Linux](#linux) — three ways to get it. Pick one.
- [Windows](#windows) — one PowerShell line, or the zip by hand.

## Linux

### Option 1 — one command (easiest)

```bash
curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash
```

That downloads the latest release into `~/sholto`, installs the runtime tools it
needs (ffmpeg, Beat This!, and a couple of system libraries — it will ask for your
password once), and launches Sholto. Run the same command again later to update.
Set `SHOLTO_DIR=/somewhere` to install elsewhere, or add `--no-run` to install
without launching:

```bash
curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash -s -- --no-run
```

Prefer to see what you're running first? Download the script, read it, then
`bash get-sholto.sh`.

### Option 2 — download the release yourself

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

### Option 3 — build from source

```bash
git clone https://github.com/freedomfirst26/Sholto.git
cd Sholto
bash install.sh
dotnet run -c Release --project src/Sholto.Host
```

`install.sh` sets up everything Sholto needs — including .NET and all the tools
below — and is safe to re-run. It works on modern Ubuntu, Mint, Pop!_OS, and
Debian.

## Windows

You need **Windows 10 or 11, 64-bit**. No admin rights and no .NET needed — the
download is self-contained.

### One line in PowerShell (easiest)

Open **PowerShell** (Start menu → type *PowerShell*) and paste:

```powershell
irm https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.ps1 | iex
```

That downloads the latest release into `%LOCALAPPDATA%\Sholto\app`, installs the
runtime tools it needs (below), adds **Sholto** to the Start menu, and launches it.
Run the same line again later to update; your library and analysis next to `app\`
are kept. To install somewhere else, or without launching, set one of these first,
in the same window:

```powershell
$env:SHOLTO_DIR = "D:\Sholto"   # install here instead of %LOCALAPPDATA%\Sholto
$env:SHOLTO_NO_RUN = "1"        # install without launching
```

### Or download the zip yourself

1. Download `sholto-windows-x64.zip` (or the versioned
   `sholto-vX.Y.Z-windows-x64.zip`) from the
   [**latest release**](https://github.com/freedomfirst26/Sholto/releases/latest).
2. Right-click it → **Extract All…** into a folder of its own.
3. In that folder, install the runtime tools once:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File install-deps.ps1
   ```

4. Double-click `Sholto.exe`.

The zip holds `Sholto.exe`, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
`docs/license.md`, `install-deps.ps1`, `sholto-deps.ps1` (the tool versions and
checks `install-deps.ps1` uses) and `get-sholto.ps1`.

### "Windows protected your PC"

Sholto's exe is not code-signed yet, so the first time you start it Windows
SmartScreen may show a blue **"Windows protected your PC"** box. Click
**More info**, then **Run anyway**. Windows remembers the choice for that copy of
Sholto.

### What `install-deps.ps1` installs

`get-sholto.ps1` runs it for you; it is safe to re-run, and skips anything that
already works.

- **ffmpeg** (required) — the *shared* build, `Gyan.FFmpeg.Shared`, through
  **winget**. If winget is missing, install **App Installer** from the Microsoft
  Store and run it again.
- **uv** — installs the Python analysis tools in their own isolated environments,
  without touching any Python you already have.
- **Beat This!** (required) — finds the beats; a track can't play without them.
- **demucs** (optional) — stem separation. Add `-NoStems` to skip it.

Add `-Verify` to only check that the tools really work:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File install-deps.ps1 -Verify
```

### Where Sholto keeps things on Windows

- Your library database: `%LOCALAPPDATA%\sholto\library.db`
- Analysis cache: `%LOCALAPPDATA%\sholto`
- Your own themes: `%APPDATA%\sholto\themes`

On Linux these stay where they always were (`~/.local/share/sholto/library.db`,
`~/.config/sholto/themes`).

### What's different on Windows

- The **DDJ-FLX4** is picked up over USB as on Linux (Sholto talks to it through
  Windows' built-in MIDI).
- Playing the master to **separate speakers** while the headphone cue stays on the
  controller is **Linux only** for now (it relies on PipeWire). On Windows, Sholto
  plays through the one output device you pick.

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
for you on Ubuntu / Mint / Pop!_OS / Debian; `install-deps.ps1` does the same on
Windows. On another distro, install the equivalents:

**Required**
- **.NET 10** — to build and run Sholto from source. The prebuilt binary does
  not need it.
- **ffmpeg** — decodes your audio for the beat detector, and also decodes
  M4A/AAC files for playback.
- **Beat This!** (the `beat-this` package) — finds the beats and tempo. Sholto
  can't play a track until it has a beatgrid, so this one isn't optional. The first
  time it runs it downloads its model (about 78 MB). Its code and model are both
  MIT-licensed, so it's fine at paid gigs.
- A normal **Linux desktop** (X11 or Wayland) and a working **sound system**
  (PipeWire, PulseAudio, or ALSA) — or **Windows 10 or 11, 64-bit**.

**Optional** — Sholto runs fine without these, you just lose that one feature:
- **demucs** — stem separation (drums / vocals / bass / other). Without it, the
  stem chips and the hot-cue stem mutes are unavailable.
- **madmom** (the `madmom-onnx` build) — the beat finder Sholto used before Beat
  This!. The installers no longer install it. Sholto uses it only if Beat This!
  is missing and you installed madmom yourself; with Beat This! installed you lose
  nothing without it. Its models are for non-commercial use only — see below.

**Checking they actually work.** The background tools are ordinary Python
programs, and an update to one of them can leave it installed but broken. Run
`bash install.sh --verify` (or `bash install-deps.sh --verify` if you're on the
prebuilt binary, or `install-deps.ps1 -Verify` on Windows) and Sholto will run each one for real — it finds the beats in a
test click track and separates a test clip into four stems, rather than just
checking the program is there. It tells you which one is broken and what you lose without it.

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
| **Beat This!** (`beat-this`) | Listens to a track and works out where every beat and every bar starts. This is the foundation everything else stands on — sync, looping, the grid on the waveform, quantised cues. | **Required.** A track can't play without a beatgrid (unless madmom, below, is installed instead). |
| **madmom** (`madmom-onnx`) | The older beat finder. Optional, not installed by default; used only when Beat This! is missing. | Nothing, as long as Beat This! is installed. |
| **demucs** | Splits a finished track back into four separate recordings — drums, bass, vocals, everything else — so you can drop the vocal out of one track while keeping its drums. | Stem chips and hot-cue stem mutes are unavailable. |
| **ffmpeg** | The universal audio translator. Converts M4A/AAC files into something Sholto can play, and prepares audio for the beat detector. | M4A/AAC files won't load. |

**Libraries built into Sholto** — code compiled into the app itself. You never
install these separately; they arrive with it.

| Library | What it does for you |
|---|---|
| **Avalonia** | Draws the entire interface — decks, waveforms, library, every control. It's what lets Sholto look the same on any Linux desktop and on Windows. |
| **NAudio.Midi** | Talks to your DDJ-FLX4 on Windows. |
| **SoundFlow** | Pushes audio out to your speakers, and is what makes the pitch and tempo changes sound right rather than chipmunky. |
| **NAudio** + **NLayer** | Read WAV, FLAC and MP3 files and turn them into the samples the decks play. |
| **z440.atl.core** | Reads the artist, title, album and artwork embedded in your files, so the library list isn't just filenames. |
| **SQLite** + **Entity Framework Core** | Remember everything between sessions — your analysed tracks, cue points, crates, tags and ratings. |

**A note for anyone licensing Sholto commercially.** The programs in the first
table are deliberately kept at arm's length — Sholto launches them as separate
processes and talks to them through files and command output. None of their code
is linked into Sholto. That boundary is intentional, because those programs carry
their own licences that differ from Sholto's. See
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). Beat This!, the required
beat finder, is MIT-licensed for both its code and its trained model. madmom, the
optional fallback, has non-commercial trained models, and a commercial licence to
Sholto does not carry a right to use them — it is only ever run if you install it
yourself and leave Beat This! out.
