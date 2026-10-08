# Changelog

Release notes for Sholto. The **Unreleased** section collects everything since
the last tag; when we cut a release it becomes that version's notes and is
pasted into the GitHub Release. Written for DJs, not developers — say what
changed at the decks, not which class moved.

## Unreleased

### Licence

- **Sholto moves to the PolyForm Shield License 1.0.0.** Free for everyone, including paid gigs, streaming, teaching, and clubs or bars running it for their DJs. Anything that competes with Sholto (selling copies, rebranding, hosting it, or bundling it with hardware or another product) needs a commercial licence: freedomfirst26@proton.me. Earlier releases keep the licence they shipped with (v0.1.0 and v0.2.0: PolyForm Noncommercial 1.0.0 with a free grant for individuals; v0.3.0 and v1.0.0: Business Source License 1.1). See [docs/license.md](docs/license.md).

### New

- **Sholto has a system tray icon.** The same tri-colour S as the window. Click it, or choose Show Sholto, to bring the window forward; Quit closes Sholto. Closing the window still quits. On a desktop without a tray, nothing changes.
- **Search ranks tracks by how well they fit your mix.** Press Space and the list is sorted against the deck you're not loading into: green for a good key and tempo match, amber for usable, grey for a clash. Find tracks by initials, `bpm:128`, `key:8a` or `#tag`; click a row's list mark to add it to the Track List or take it out, and see your crates and tags beside the results. Shift+1 / Shift+2 load straight onto a deck; LOAD on the controller loads the pick.
- **Search loads into three places: Deck 1, Deck 2 and the Track List.** Ctrl+L adds the highlighted song, crate or tag to the main view's Track List and keeps search open; loads add and never replace, the strip above the list shows the sources (× removes one), and the list survives a restart. Esc on the main view now only closes the tuner.
- **The violet list mark on a search row adds the song to the Track List, or removes it.** It replaces the shortlist and the Q key: the mark shows a tick (≡✓) while the song is in the list, Ctrl+L does the same on the highlighted row, the row flashes when it is added, the footer counts the list, and any songs you had shortlisted move into the Track List the first time you start.
- **Click a crate or tag in search to narrow the list to it, ranked by what fits the playing track.** Picks become chips in the search box and stack; Backspace or the × takes one off. Chips only narrow search.
- **The search header shows both decks.** The two deck slots now carry a platter that spins with the music, a ring of the time left (red in the last 45 seconds), the title, key, BPM and time. The deck a load goes to is washed in its colour. Click a slot to load onto it. An amber warning shows when the deck you would load onto is playing, and red "again to replace" after the first press.
- **Undo a load.** Ctrl+Z within 10 seconds of loading puts the previous track back on that deck, paused where you replaced it.
- **The section map shows the whole track.** The strip above each waveform is taller: section names over a coloured underline, and below them the entire song drawn in your waveform style (3-BAND or RGB) with the played part dimmed, and your cues and loop marked on it.
- **An empty deck shows a ghost vinyl.** With no track loaded, the platter is drawn as a faint outline of a record, so you can see at a glance which deck is waiting for a load.
- **The deck disc glows with the music.** Behind the BPM, soft bass, mid and high glows in your theme's waveform colours swell and fade with what is playing; the disc goes dark when the deck is empty.
- **Choose how your waveforms look.** Settings ▸ Layout Wizard… lets you pick the deck waveform style:
  **3-BAND** (bass, mids and highs as layers, as before) or the new **RGB** (Rekordbox-style fine stripes, each
  coloured by what is playing at that instant). The decks change as you pick; Esc
  puts them back. Sholto remembers your choice.
- **Try themes on in the Layout Wizard.** The wizard now starts with a theme step: every theme is a small preview card
  (your own themes from the themes folder get their own group). Pick one and the whole app changes to it straight
  away; the waveform step then shows both styles in that theme. Nothing is kept until you press Apply, and Cancel
  or Esc puts back what you had.

- **Set how long and how far a backspin goes.** Settings ▸ Settings… has two Platter feel knobs. Backspin time is how long the platter keeps spinning after you let go (0.6 seconds to start). Backspin distance is how far a firm spin rewinds, in beats (2 to start), and harder spins go a little further. Turn either to 0 and the platter stops dead. Forward flings follow both knobs. Changes apply to the next fling and Sholto remembers them. Your old single release setting is replaced by these two, which start at their defaults.

  ![Settings, Platter feel: the Backspin time and Backspin distance knobs](pictures/settings-platter-feel.webp)


### Improved

- **Choosing your audio output now looks like the rest of Sholto.** The picker opens inside the window in your theme. It says whether this is your first start or your saved device is unplugged, and marks the device in use and the system default. Use ↑↓, Enter and Esc, or click.
- **Track List rows catch a soft sheen of light that follows your pointer.** Nothing moves or resizes; with reduced motion the light just rests on the title.

- **Stem levels now use SHIFT + EQ.** Hold a deck's SHIFT and turn its HI, MID or LOW knob to set that deck's drums, vocals or instrumental level. FX ON/OFF no longer does this, and does nothing in Sholto.
- **Empty deck slots in search are clearer.** A deck with no track now shows as an empty slot, so you can see which deck a load will go to and which is waiting.

- **Loading onto a playing deck now asks for a second press.** The first press shows a warning; press again within 3 seconds to replace the track. This covers the keyboard, the screen and the controller's LOAD buttons, so a stray press can't cut your music.
- **"Birthday Massacre" is now "The Birthday Massacre".** The theme is called by its proper name in the picker, and if you had it selected it stays selected.
- **Song sections now line up with the music's phrases.** Each section on the map shows its length in bars (like DROP 16), builds are no longer mistaken for drops, and loud pop tracks aren't all labelled DROP.
- **Closing the controller guide now shrinks it into its button.** The guide folds into the controller icon in the top bar, which pulses for a moment so you know where to reopen it. If your desktop has animations turned off, it simply fades.

### Fixed

- **The controller-guide hint now stops after its first few showings** instead of coming back every time you launch Sholto.
- **Settings > Output device... no longer marks the speakers in use when master could not be routed there, and picking them again retries.**
- **Key chips are easier to read.** Key chips now have white text and stem chips black text, and a theme can set each one. Dimmu Borgir's key chips are coloured instead of all the same grey.
- **Waveform bands are no longer identical.** Since 1.0 the bass, mid and high bands came out the same, so 3-BAND looked like nested copies and RGB was a single colour. Fixed; each track is re-analysed once the first time you load it.

## v1.0.0

Sholto 1.0 is a rebuild from the inside. The app, the controller and the screen
now talk through one channel instead of three, so at the decks it should feel the
same as before — just steadier. The controller's lights always match what's
actually happening, and a replugged FLX4 comes back the way you left it. It also
means other controllers can be added as nothing more than a new mapping. One
change you'll notice outside the decks: the program is now called `Sholto` — launch
it as `Sholto`, and the installer takes care of the rest.

### New

- **The status light tells you when something's missing.** The little dot in the
  top-right corner turns amber when one of the programs Sholto leans on for
  analysis isn't installed — click it and you get a plain list of what's there,
  what isn't, what you lose without it and the one command to fix it. A
  disconnected controller still wins: the dot stays red and says "Reconnect USB"
  until the unit is back, because that's the one you can fix in seconds.

- **A track that won't load now says so.** If a song can't be decoded when you
  load it onto a deck, a short message at the bottom of the screen names the
  track and the deck; the deck stays usable.

### Improved

- **Replugging the FLX4 puts every light back.** Play, cue, master cue, stems,
  echo and pad mode all light up as you left them the moment the controller
  reconnects, without you pressing anything.

- **Stem and echo pad lights follow the screen.** Change a stem or the echo with
  the mouse and the pad lights on the controller follow, instead of waiting for
  your next press.

- **Themes can colour everything.** Every colour in the app can now come from a
  theme file, down to the stem colours, status dot, tags, controller picture and
  the waveform's vocal lane. Existing themes keep working unchanged — anything a
  theme leaves out falls back to the standard look. `docs/themes.md` lists the
  new optional keys.

### Fixed

- **Waveform bass, mid and high colours now show the real music.** Since 1.0 all three layers were drawn from the same signal, so every track looked alike. Each track re-analyses once the next time you load it.

- **CUE no longer flips its light in the guide.** Pressing CUE while the
  controller guide is open no longer flips the CUE light.

- **Pad mode snaps back after the guide.** If you press a pad-mode button while
  the guide is open, the pad lights go back to the mode you were in when you close it.

- **Search and crate pickers close when you choose.** The search overlay and the
  crate picker now close as soon as you pick a tag or a crate, instead of staying
  open over the library.

- **The audio device picker follows your theme,** instead of staying in the
  default colours.

- **No more purple flash at startup.** The window used to show a purple frame
  before the theme loaded; it now opens in your theme's colours.

- **Key-chip glow on Classic is a touch off-white.**

- **A broken beat tracker no longer earns a "Done" banner.** The installer
  used to warn and carry on if any Python tool failed to install — right for stem
  separation and AI song sections, which are optional, but wrong for the beat
  tracker: without it, no track gets a beatgrid and nothing plays. Now, if the beat
  tracker specifically is broken, the installer says so plainly, tells you nothing
  will play, and exits with an error instead of claiming success. `install.sh`
  still finishes building Sholto first — so you have a binary to retry against —
  but reports failure, not "Done", when it's finished.

- **Analysis that fails now says so.** When one of the background tools dies on a
  track, the library shows an amber **!** where the tick would be instead of leaving
  the row blank — so a track that broke no longer looks like one nobody has got
  round to yet. Hover the **!** and you get the tool's own error, word for word,
  instead of a bare exit code.

- **Stems stop vanishing after a reinstall.** An update to one of the background
  tools Sholto uses for stem separation had quietly broken it: tracks appeared to
  analyse normally but produced no drums, vocals or instrumental. The installer now
  sticks to a set of tool versions that has been tested end to end, and checks the
  separator really produces four stems — with the exact names Sholto looks for — before
  declaring itself done. `./install.sh --verify` re-runs that check any time, names the
  program it actually tested, and says plainly what you lose if one is broken.

- **The installer no longer bails out over an optional tool.** If stem separation or
  AI song sections failed to install, the whole install used to stop dead — even
  before Sholto itself got built — so a machine where one optional tool wouldn't
  build couldn't install Sholto at all. Now a failed optional tool just gets a
  warning and the install carries on; a genuinely required tool failing still warns
  clearly instead of dying with a raw Python error.

- **A re-analysed track that fails no longer keeps its green tick.** If you
  re-analysed a track and the beat detector broke, the track could still show fully
  analysed (green tick) because it remembered BPM and stems from a previous run —
  hiding that the beatgrid you were about to mix on never actually got rebuilt. A
  broken beat detector now shows the failure marker every time, even on a track
  that looks otherwise complete; a failed *optional* step (AI song sections) still
  leaves the tick alone.

## v0.3.0

### New

- **A picture of your controller, with every button explained.** Click the controller
  icon in the top bar and your DDJ-FLX4 appears on screen. Click any knob, pad or button
  to see every way Sholto uses it — including the combinations, like holding Shift. The
  small printed labels on the board answer to a click too, so you can aim at the word
  **SHIFT** rather than the button next to it, which on a small screen is the easier
  target.
- **Press it on the real thing and watch it light up.** With the controller plugged in,
  the guide follows your hands: press a pad and it blinks on screen and tells you what it
  does. **Your decks stay silent while the guide is open**, so you can press PLAY to find
  out what PLAY does without starting the track. Close the guide and everything — including
  the lights on the unit and whatever you had cued — goes back exactly as it was.
- **You can see what Sholto doesn't use.** Controls it acts on are tinted; controls it
  ignores are left plain. No legend to read — TRIM and the FX section simply aren't lit,
  and clicking one says so plainly rather than pretending.
- **One-command install.** `curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash`
  downloads the latest release, installs its tools, and launches it. Re-run to update.
- **F11 toggles fullscreen.**

### Improved

- **Shorter actions menu.** The Enter menu on a track no longer repeats "Load to Deck 1 / 2"
  — **1** and **2** in the track list already do that.
- **Recently used tags come first.** The tag box and the search overlay list the tags you
  picked most recently at the top, so a tagging run stops meaning a lot of typing. The list
  resets when you restart Sholto.

### Fixed

- **Stem levels from the EQ knobs work again.** Hold **FX ON/OFF** and the HI, MID and LOW
  knobs set the drums, vocals and instrumental levels instead of the EQ. The button had been
  listening on the wrong channel, so it had never done anything at all.
- **The BEAT SYNC light comes back on.** The controller's play light had stopped following
  the deck, so a playing deck looked stopped on the unit.
- **The tag box takes the keyboard straight away.** Open the tag editor and type — no click
  needed first.

### Housekeeping

- **Sholto is now under the Business Source License 1.1.** Free for individuals, including
  paid gigs. Organisations need a commercial licence. Every released version becomes
  Apache-2.0 on its change date, so nothing is locked away forever. See
  [LICENSE](LICENSE).
- Added contributing guidelines and third-party notices.
- Rewrote the guide under `docs/` to cover what's on screen. The controller is documented
  in the app itself now, so there is one place to look and it cannot drift out of date.

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
