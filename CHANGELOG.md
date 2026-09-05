# Changelog

Release notes for Sholto. The **Unreleased** section collects everything since
the last tag; when we cut a release it becomes that version's notes and is
pasted into the GitHub Release. Written for DJs, not developers — say what
changed at the decks, not which class moved.

## Unreleased

## v0.2.0

### New
- **Three new themes:** Dimmu Borgir, Aphex Twin, and The Prodigy.
- **M4A / AAC playback.** Tracks in `.m4a` (AAC) now show up in the library and
  play. Decoded through ffmpeg, which Sholto already needs.
- **Shift + CUE restarts the track** from the very beginning on either deck,
  keeping it playing if it was playing.
- **Hot Cue / Pad FX pages.** The HOT CUE and PAD FX1 mode buttons switch each
  deck's pads between hot cues and effects.
- **Beat-synced echo** on PAD FX1 pad 1. Switching it off lets the tail ring out
  (the classic echo-out) instead of cutting dead.
- **Whole-song section map** above each waveform: intro, build, drop, breakdown
  at a glance.
- **Master output to a second sound card** with a MASTER CUE monitor in the
  headphones (Linux / PipeWire).
- **Vinyl platter**: scratch, backspin, brake-to-pause, and Shift + platter fast
  search on the DDJ-FLX4.

### Improved
- **Platter feel.** The platter's touch sensor now drives scratching, so a hand
  resting on the top holds the deck and lifting it releases instantly; no more
  elastic bounce mid-scrub. Releasing after a normal scrub resumes from exactly
  where you let go; only a genuine spin coasts on, and a spinback dies out
  twice as fast.
- **Beatgrid** is a least-squares fit through every detected beat, so the grid
  stays locked across the whole track.
- **Waveform** scaling preserves dynamics between decks; bells are flatter with
  a slower fall-off.
- All platter-feel numbers live in one settings file for easy tuning.
- **Transport is controller-only.** Clicking the section map no longer jumps the track; the platter, CUE, and Shift + CUE are the way to move.
- **Section map is slimmer and follows your theme.** The strip above each
  waveform is about half as tall, and its section colours now come from the
  active theme. Themes can set them explicitly with a `minimap` section, or
  leave it out and get colours derived from their accent, primary, and mint.
- **Waveform follows your theme.** Every theme now colours the waveform's
  bands, grid, playhead, markers, loop band, and gain line in its own
  palette. Themes set them in a `waveform` section; anything left out
  derives from the theme's accent and mint. The inner band stays white on
  every theme so the vocal markers always read.
- **Theme picker shows each theme's colours** next to its name.

### Fixed
- **Side-ring nudges and beat-snap no longer jump the deck** to an earlier
  point after a scratch or when the tempo fader is off centre. The seek was
  reading a stale clock.
- Grid-nudge targeted the wrong deck in some cases.

### Housekeeping
- Licence: dual PolyForm Noncommercial + free individual grant, with a credit
  requirement for published forks. Copyright holder is the project handle.
- `install-deps.sh` for prebuilt-binary users; README offers the prebuilt
  download alongside build-from-source.
- Removed the Drab Majesty, Sub Focus, and Boards of Canada themes.

## v0.1.0

First public build.
