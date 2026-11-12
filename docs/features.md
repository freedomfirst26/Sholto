# What Sholto does

The full feature list. For what each part of the screen means, see
[the guide](README.md).

## Your library
- Finds every track in your music folder — **mp3, FLAC, WAV, AIFF, and M4A/AAC** — and reads the artist, title, and other tags automatically.
- Shows everything in a sortable list: **Artist · Track · BPM · Key · Time**.
- Analyses each track once and remembers the result, so it's instant every time after.
- **Analyse your whole library in one go** — Settings ▸ Analyse whole library… works out the beats and key of every song not analysed yet, a couple at a time in the background, with a running count and a summary at the end. Settings ▸ Cancel library analysis stops it. Stems are still made when you load a track.
- **Choose where the stems go** — Settings ▸ Folders… shows two rows, **Music folder** and **Analysis folder**, each with its own **Change…** button. The stems (about 175 MB per track) go in the analysis folder, so you can put them on a bigger drive; a new install suggests a **Sholto Analysis** folder next to your music. Change it later and Sholto moves the stems already made, with progress, so nothing is analysed again. If a drive isn't connected at start, Sholto asks instead of writing somewhere else.

  ![The Folders panel: the Music folder and Analysis folder rows, each with a Change… button](../pictures/folders-panel.webp)

More: [library.md](library.md).

## Finding and organising
- **Search that fits your mix** — press the spacebar and type. Search ranks your tracks by how well they fit the deck you're *not* loading into: the header shows that deck's key, BPM and title. Each track is marked green (a good fit), amber (usable) or grey (a clash) from its key and tempo, with no bar until it is analysed, and half- and double-time count. Tracks you haven't loaded yet come first. With nothing loaded on the other deck (or its key and BPM not known yet), the list is simply A to Z by artist.
  - Type a few letters of a title or artist, or just the initials (`iltwykm` finds "I Like The Way You Kiss Me", `bsn` finds "Born Slippy .NUXX"). Add `bpm:128` (or `bpm:124-128`), `key:8a` or `#vocal` to narrow it down; every word you type has to match. Every rule is in [search.md](search.md).
  - Click the violet list mark on a row (**≡+**, or **≡✓** once it's in) to add that song to the Track List or remove it, or press **Ctrl + L**. A side list holds your crates and tags.
  - Search always covers your whole library. To narrow search itself, use a chip (below). It shows the best 250 matches.
- **Narrow search to a crate or tag** — click a crate or tag in the side list (or press **Enter** on it) and it becomes a chip in the search box; the list narrows to it, still ranked by fit. Chips stack (a crate *and* a tag), the side list marks the ones in use and shows how many tracks each would leave, and typed words filter on top. Click a chip's **×**, or press **Backspace** in an empty box, to take it off. Chips only narrow search; they don't filter the main list. If a re-rank takes a moment, the old list dims and a light runs down the fit bars; with reduced motion it just dims.

  ![The search box holding a crate chip and a tag chip, with those two marked in the side list](../pictures/glance-chips.webp)

- **Load straight from search** — **Enter** loads onto the deck shown, **← / →** switches deck, and **Shift + 1** / **Shift + 2** load onto Deck 1 / Deck 2 directly.
- **The Track List** — the main view's list. **Ctrl + L** in search, or the violet **Load** button beside the search box, adds the highlighted song, crate or tag to it and leaves search open so you can add more. Once it's in, the button shows a tick and reads **In list**; click it again to take it back out. With nothing highlighted the button is greyed out. Loads add and never replace, and a song is never listed twice. The strip above the list shows what you've loaded; **×** removes the songs that source brought in. Drag the grip to reorder, press **Delete** to remove the highlighted song, or press **Ctrl + Del** twice in search to clear it. It is kept when you restart Sholto, and holds **All Tracks** on first launch.
- **See both decks while you search** — the two deck slots in the header each show a spinning platter (one turn per bar, stopped if your desktop asks for reduced motion) with a ring of the time left (red in the last 45 seconds), the title, key, BPM and time left. Click a slot to load the highlighted song onto it. The deck a load goes to is washed in its deck colour, with a notch on top; the platter reads **⇧1** / **⇧2**, the key that loads there; if it is playing the slot turns amber, and after the first press it turns red and reads "again to replace".

  ![The deck slots in the search header: one deck playing with the other left as an empty slot and chosen as the target, then the playing deck chosen and flagged amber](../pictures/glance-slots.webp)
- **Asks before replacing a playing deck** — loading onto a deck that is playing shows a warning; load again within 3 seconds to confirm. This applies to the keyboard, the screen and the controller's LOAD buttons.
- **Undo a load** — **Ctrl + Z** within 10 seconds of a load puts the previous track back on that deck, paused at the spot where you replaced it. (EQ, filter and loop settings are not restored.)
- **Tags** — label any track with words that matter to you ("peak time", "vocal", "drum & bass"), then filter the whole library down to a tag in one click.
- **Crates** — group tracks into crates (like playlists or record boxes) and jump to a crate's contents instantly. An **All Tracks** crate always holds everything; **Ctrl + L** in search adds a crate's songs to the Track List.

## Reading your tracks
- **Automatic beat and tempo detection** — Sholto finds the BPM and marks the downbeats so your beatgrid lines up.
- **Key detection** — every track gets its musical key (with the Camelot code) so you can mix in harmony.
- **Stem separation** — splits a track into its parts (vocals, drums, bass, and the rest) so you can drop out the vocal or bring back the beat, live.
- A small progress bar shows a track being analysed, and a check mark when it's ready.

## Playing and mixing
- **Two decks** you can play, scrub, and mix independently.
- **3-band EQ** on each deck (highs, mids, lows) — cut a band all the way to silence, like a hardware isolator.
- **Filter knob** per deck — sweep from a low-pass to a high-pass for that classic build-up-and-drop feel.
- **Headphone cue** — pre-listen a track in your headphones while the crowd still hears the other deck.
- **Beat loops** — set a loop on the beat and halve or double its length on the fly.
- **Magnetic beat-snap** — when both decks are playing and the beats drift close together, the jog wheel gently "holds" on the beat and both waveforms glow green; let go and the deck locks to the other one's grid. No button to arm — it just happens. More: [deck.md](deck.md#magnetic-beat-snap).
- Pick which speakers or headphones Sholto plays to (it asks on first start, or **Settings ▸ Output device…**), and it remembers your choice. Your connected controller is listed first, as "Controller (<card name>)", so you can send the master mix back to it after picking speakers.
- Sholto shows its icon in the system tray; click it to bring the window back.

More: [deck.md](deck.md).

## Seeing your tracks
- **Waveforms coloured by frequency** — deep bass, mids, and highs each get their own colour, and the height shows how intense each moment is, so you can spot the intro, the build-up, the drop, and the breakdown at a glance.
- **Two waveform styles**, chosen in **Settings ▸ Layout Wizard…**: **3-BAND** (the three bands drawn as layers) or **RGB** (Rekordbox-style fine stripes, each coloured by the mix of bass, mids and highs playing at that instant). See [waveforms.md](waveforms.md).
- **Beat-grid markers** along the top, with the downbeats highlighted.
- **A spinning vinyl disc** per deck that turns in time with the track, its ring shading from green to red as the track plays out and flashing near the end. Behind the BPM in the middle, soft bass, mid and high glows in your theme's waveform colours swell and fade with the music.
- **Stem chips** under each disc (drums / vocals / instrumental) — lit when you can hear that part, hollow when it's muted.
- The deck dims red when its volume is all the way down.
- **Eleven colour themes**, plus your own. The Layout Wizard's first step lets you try every theme on the whole app before you apply it; **Settings → Theme** switches straight away. See [themes.md](themes.md).

## Your DDJ-FLX4

Plug it in and it works. Click the controller icon in the top bar for a picture of
your DDJ-FLX4 with every knob, pad and button explained, including the Shift
combinations. Press anything on the real controller and the guide lights up the
matching control, without touching your decks. Controls Sholto doesn't use are
left uncoloured, so you can see at a glance what's live.

Close the guide (Esc, or its × button) and it shrinks into the controller icon, which pulses so you know where to reopen it; with your desktop's animations off it just fades.

**The pad pages.** The HOT CUE, PAD FX1, BEAT JUMP and SAMPLER buttons switch a deck's eight pads between four jobs. Hold **SHIFT** while you press one of those buttons to reach its second page: BEAT LOOP behind BEAT JUMP, and three pages Sholto leaves dark. The pads on the real controller are simply lit or unlit; any colours show on screen only.

- **HOT CUE** saves up to eight cue points per track. Press an empty pad to save a cue at the playhead (snapped to the nearest beat); the pad stays lit while it holds a cue. Press a saved pad to jump to it: a playing deck keeps playing, and a paused deck plays only while you hold the pad (a preview), then comes back to the cue when you let go. Press **PLAY** while you hold the pad and the deck keeps playing after you let go. **SHIFT** + pad deletes that pad's cue. Cues are saved with the track and come back when you load it.
- **PAD FX1** — hold pad 1 to echo the deck in time with the beat (lit while the echo is on); let go and the echo tail rings out. Hold pad 2 to roll a short slice of the beat over and over; let go and the track carries on where it would have been. Pads 3–8 do nothing, and **SHIFT** does nothing here.
- **BEAT JUMP** — the eight pads move the playhead 1 beat back, 1 forward, 2 back, 2 forward, 4 back, 4 forward, 8 back and 8 forward, counted in beats of the track's own tempo; all eight stay lit. **SHIFT** + pad 7 halves how far every pad jumps and **SHIFT** + pad 8 doubles it, from an eighth up to eight times the printed beats (so pad 8 can jump anything from 1 to 64 beats); the size goes back to normal each time Sholto starts. **SHIFT** + the other pads does nothing.
- **SAMPLER** — pads 1–3 mute and bring back the drums, vocals and instrumental stems (lit while you can hear that part); pads 5–7 solo them (lit while that part is the only one you hear), and pressing the soloed pad again brings all three back. Pads 4 and 8 do nothing, and **SHIFT** does nothing here. The stems need the track's stem analysis to have finished.
- **BEAT LOOP** (**SHIFT** + **BEAT JUMP**) — the eight pads start a loop of 1/4, 1/2, 1, 2, 4, 8, 16 or 32 beats, snapped to the beat; while a loop is running, any of them ends it. All eight stay lit.
- **KEYBOARD** (**SHIFT** + **HOT CUE**), **PAD FX2** (**SHIFT** + **PAD FX1**) and **KEY SHIFT** (**SHIFT** + **SAMPLER**) are dark: Sholto gives their pads no job yet, so pressing them does nothing.

**Browsing with the controller.** Turning the browse knob scrolls the Track List, and **LOAD 1** / **LOAD 2** loads the highlighted track (or the search pick while search is open). If the deck is playing, press **LOAD** a second time within 3 seconds to replace it. Hold the knob for about a second to re-analyse the highlighted track.

![The controller guide — the DDJ-FLX4 drawn on screen with the jog wheel selected, and a panel explaining every way of using it](../pictures/sholto-faceplate.webp)

**Platter feel**, two knobs in **Settings ▸ Settings…**, sets how a fling on the jog wheel behaves
when you let go. **Backspin time** is how long the platter keeps spinning (0.6 seconds to start).
**Backspin distance** is how far a firm spin rewinds, counted in beats (2 to start, so it follows
the track's tempo); harder spins go a little further. Turn either to 0 and the platter stops dead.
Forward flings follow the same two knobs. Drag or scroll a knob (or use the arrow keys, Tab to switch
knob); double-click puts it back to its start value. Sholto remembers both.

![Settings, Platter feel: the Backspin time and Backspin distance knobs](../pictures/settings-platter-feel.webp)

Adding support for another controller is straightforward — Sholto keeps each
device's button layout in one place.

## Keyboard

- **Space** — open search (ranks tracks by fit against the deck you're not loading into; type to narrow, or search crates and tags).
- **Shift + 1 / Shift + 2** (in search) — load the highlighted track straight onto Deck 1 / Deck 2.
- **← / →** (in search) — choose which deck **Enter** loads onto.
- **Tab** (in search) — switch between the track list and the side list (crates, tags).
- **Enter** (in search, on a crate or tag) — add or remove it as a chip. **Ctrl + L** adds the highlighted song, crate or tag to the Track List. **Ctrl + Del** twice clears it. **Backspace** in an empty search box removes the newest chip.
- **Ctrl + Z** — undo the last load (within 10 seconds).
- **↑ / ↓** — move up and down the track list.
- **1 / 2** — load the highlighted track onto Deck 1 or Deck 2.
- **Enter** — open the actions menu for the highlighted track (add it to a crate, or tag it).
- **P** — play / pause Deck 1; hold **Shift** for Deck 2.
- **G** — open the beatgrid / tempo tuner on the playing deck; then **↑ / ↓** change the tempo and **← / →** nudge the grid.
- **M** — drop a marker on the playing deck.
- **C** — send Deck 1 to your headphones (cue), or take it off again; hold **Shift** for Deck 2. Same as the controller's headphone buttons.
- **Esc** — close the tuner.
- **F11** — toggle fullscreen.

The full table, with the Shift variants, is in [the guide](README.md#keyboard-at-a-glance).
