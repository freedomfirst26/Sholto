<img src="pictures/sholto-icon.svg" width="120" align="right" alt="Sholto"/>

# Sholto

DJ software for mixing your own music — a free alternative to Rekordbox and Serato.

**Status:** runs on **Linux** with the **Pioneer DDJ-FLX4** controller today. Windows, macOS, and more controllers are on the way.

![Sholto — library on top, two decks below with section maps, live waveforms, and spinning discs](pictures/sholto-ui.png)

## What it does

### Your library
- Finds every track in your music folder — **mp3, FLAC, WAV, AIFF, and M4A/AAC** — and reads the artist, title, and other tags automatically.
- Shows everything in a sortable list: **Artist · Track · BPM · Key · Time**.
- Analyses each track once and remembers the result, so it's instant every time after.

### Finding and organising
- **Search** — press the spacebar and type. Sholto searches your **tracks, crates, and tags** all at once and shows the top few matches of each, so you can see everything you can jump to.
- **Tags** — label any track with words that matter to you ("peak time", "vocal", "drum & bass"), then filter the whole library down to a tag in one click.
- **Crates** — group tracks into crates (like playlists or record boxes) and jump to a crate's contents instantly. An **All Tracks** crate always holds everything; press **Esc** to get back to it.

### Reading your tracks
- **Automatic beat and tempo detection** — Sholto finds the BPM and marks the downbeats so your beatgrid lines up.
- **Key detection** — every track gets its musical key (with the Camelot code) so you can mix in harmony.
- **Stem separation** — splits a track into its parts (vocals, drums, bass, and the rest) so you can drop out the vocal or bring back the beat, live.
- A small progress bar shows a track being analysed, and a check mark when it's ready.

### Playing and mixing
- **Two decks** you can play, scrub, and mix independently.
- **3-band EQ** on each deck (highs, mids, lows) — cut a band all the way to silence, like a hardware isolator.
- **Filter knob** per deck — sweep from a low-pass to a high-pass for that classic build-up-and-drop feel.
- **Headphone cue** — pre-listen a track in your headphones while the crowd still hears the other deck.
- **Beat loops** — set a loop on the beat and halve or double its length on the fly.
- **Magnetic beat-snap** — when both decks are playing and the beats drift close together, the jog wheel gently "holds" on the beat and both waveforms glow green; let go and the deck locks to the other one's grid. No button to arm — it just happens.
- Pick which speakers or headphones Sholto plays to, and it remembers your choice.

### Seeing your tracks
- **Waveforms coloured by frequency** — deep bass, mids, and highs each get their own colour, and the height shows how intense each moment is, so you can spot the intro, the build-up, the drop, and the breakdown at a glance.
- **Beat-grid markers** along the top, with the downbeats highlighted.
- **A spinning vinyl disc** per deck that turns in time with the track, its ring shading from green to red as the track plays out and flashing near the end.
- **Stem chips** under each disc (drums / vocals / instrumental) — lit when you can hear that part, hollow when it's muted.
- The deck dims red when its volume is all the way down.
- **A range of colour themes** to switch between in Settings.

### Handy moves
- **Fine-tune the beatgrid** — click the BPM on a deck to open a little tuner: nudge the tempo up or down and slide the grid left or right until it lines up with the kicks. There's a one-tap **½ / ×2** for the common case where a slow track is read at twice its real speed, and a reset back to the detected values. Every adjustment is remembered per track.
- **Drop markers** — press **M** to mark the spot you're at on the playing deck so you can find it again.
- **Re-analyse a track** — double-click it (or hold the browse knob on it for about a second) to analyse from scratch — a rescue for the odd track whose beats came out wrong.

## Your DDJ-FLX4
- **Play / pause** and **jog wheels** (top platter to scrub fast, the side ring for fine nudges) on each deck.
- **Channel faders** and the **crossfader** for volume and blending.
- **EQ knobs** (high / mid / low) driving each deck's isolator.
- **The browse knob** scrolls the track list; **LOAD 1 / LOAD 2** load the highlighted track onto a deck.
- **Hot-cue pads** double as **stem mute toggles** — drums, vocals, instrumental — once a track's stems are ready.
- **The CUE buttons** send each deck to your headphones for pre-listening. **Shift + CUE** jumps the track back to the very start instead (keeps playing if it was playing).

Adding support for another controller is straightforward — Sholto keeps each device's button layout in one place.

### Keyboard
- **Space** — open search (type to find tracks, crates, and tags).
- **↑ / ↓** — move up and down the track list.
- **1 / 2** — load the highlighted track onto Deck 1 or Deck 2.
- **Enter** — open the actions menu for the highlighted track (tag it, add it to a crate, or load it).
- **P** — play / pause Deck 1; hold **Shift** for Deck 2.
- **G** — open the beatgrid / tempo tuner on the playing deck; then **↑ / ↓** change the tempo and **← / →** nudge the grid.
- **M** — drop a marker on the playing deck.
- **Esc** — close a menu, or clear a tag/crate filter to go back to **All Tracks**.

## Install and run

Two ways to get Sholto — grab a ready-made binary, or build it yourself.

### Option 1 — one command (easiest)

```bash
curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash
```

That downloads the latest release into `~/sholto`, installs the runtime tools it needs (ffmpeg, madmom, and a couple of system libraries — it will ask for your password once), and launches Sholto. Run the same command again later to update. Set `SHOLTO_DIR=/somewhere` to install elsewhere, or add `--no-run` to install without launching:

```bash
curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash -s -- --no-run
```

Prefer to see what you're running first? Download the script, read it, then `bash get-sholto.sh`.

The binary is **self-contained — you don't need .NET installed.** If you'd rather do it by hand, grab `sholto-*-linux-x64.tar.gz` from the [**Releases**](https://github.com/freedomfirst26/Sholto/releases) page, `tar -xzf` it, run `bash install-deps.sh` once, then `./Sholto.App`.

### Option 2 — build from source

```bash
git clone https://github.com/freedomfirst26/Sholto.git
cd Sholto
bash install.sh
dotnet run -c Release --project src/Sholto.App
```

`install.sh` sets up everything Sholto needs — including .NET and all the tools below — and is safe to re-run. It works on modern Ubuntu, Mint, Pop!_OS, and Debian.

On startup Sholto scans your music folder. Click a track to load it onto Deck 1, or press LOAD 2 on the controller to load it onto Deck 2.

### What it needs

`install.sh` installs all of this for you on Ubuntu / Mint / Pop!_OS / Debian. On another distro, install the equivalents:

**Required**
- **.NET 10** — to build and run Sholto.
- **ffmpeg** — decodes your audio for the beat detector, and also decodes M4A/AAC files for playback.
- **madmom** (the `madmom-onnx` build) — finds the beats and tempo. Sholto can't play a track until it has a beatgrid, so this one isn't optional.
- A normal **Linux desktop** (X11 or Wayland) and a working **sound system** (PipeWire, PulseAudio, or ALSA).

**Optional** — Sholto runs fine without these, you just lose that one feature:
- **demucs** — stem separation (drums / vocals / bass / other). Without it, the stem chips and the hot-cue stem mutes are unavailable.
- **allin1** — AI song-section detection (intro / build / drop / …). Without it, Sholto falls back to a simpler built-in guess.

**Your controller** — a **Pioneer DDJ-FLX4** is picked up automatically when you plug it in; a green dot in the top bar means it's connected (red means reconnect the USB), and it reconnects on its own if it drops. You don't need one — everything works from the mouse and keyboard.

## License

Dual-licensed — see [LICENSE](LICENSE):

- **Individuals — free.** Use, modify, fork, and gig with Sholto for any
  purpose, including paid DJ sets, under the
  [PolyForm Noncommercial 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0)
  plus a free individual-use grant from the author. Forks and derived works
  you publish must credit Sholto in their README and keep the copyright notice.
- **Businesses & commercial use — paid.** Any use by an organization, or
  bundling Sholto into a product or hosted service, needs a paid commercial
  license — any size. Open an [issue](https://github.com/freedomfirst26/Sholto/issues)
  to arrange one.

The goal is to keep Sholto free for individuals while asking businesses to chip in. Contributions are welcome under the same dual terms (see LICENSE) so they can ship in both editions.
