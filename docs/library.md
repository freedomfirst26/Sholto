# The library

The top half of the window. Every track Sholto found in your music folder, one
row each, newest analysis showing as it lands.

## The columns

| Column | What it shows |
|---|---|
| **ANALYZED** | A moving bar while the track is being analysed, then a tick |
| **TAGS** | A 🏷 chip with the number of tags on that track (hidden if it has none) |
| **ARTIST** | The artist tag from the file |
| **TRACK** | The title. Tracks you've loaded this session go *italic* and dim, so you can see what you've already played |
| **BPM** | The detected tempo, including any ½ / ×2 correction you made |
| **KEY** | The Camelot key, on a coloured chip. Chips that mix harmonically with what's playing get a white outline |
| **TIME** | Track length |

The tick in **ANALYZED** only appears when a track is *fully* done — tempo and
beats **and** stems. A track with beats but no stems yet still shows as
unanalysed, and it will play fine.

## Choosing a track

**On screen:** click a row to highlight it. Clicking does *not* load it.
**Controller:** turn the browse knob to move the highlight.
**Keyboard:** **↑ / ↓**.

## Loading a track

**Controller:** **LOAD 1** or **LOAD 2** loads the highlighted track.
**Keyboard:** **1** or **2**.

The deck shows the title straight away and fills in once the audio has decoded.
It won't play until the beat detection has finished — usually a second or two.

## Search

**Keyboard:** **Space** opens search from anywhere.

Type and Sholto searches your **crates**, **tracks** and **tags** at once,
showing the best three of each under 📦 CRATES, 🎵 TRACKS and 🏷 TAGS.

| Key | In search |
|---|---|
| **↑ / ↓** | Move through the results |
| **← / →** | Switch which deck **Enter** will load into |
| **Enter** | Load the track, open the crate, or filter by the tag |
| **1** / **2** | Load the highlighted track straight onto that deck |
| **Esc** | Close |

Clicking the dark backdrop closes search too.

## The actions menu

**Keyboard:** **Enter** on a highlighted row.

| Item | Key |
|---|---|
| 📦 Add to crate | **C** |
| 🏷 Tag | **T** |

**↑ / ↓** moves, **Enter** picks, **Esc** closes.

## Tags

**On screen:** click the 🏷 chip in a row's TAGS column to open the tag editor for
that track.
**Keyboard:** **Enter** then **T** does the same for the highlighted row.

In the editor, type a tag and press **Enter** to add it (**Tab** adds it and
keeps typing), **↑ / ↓** picks from the suggestions, **Backspace** on an empty box
removes the last tag, and **Esc** closes. Existing tags are listed under
**TAGGED** with an **×** to remove each one.

To see everything with a tag: press **Space**, type the tag, and press **Enter**
on it under 🏷 TAGS. The list filters down to that tag.

## Crates

Crates are your record boxes. **All Tracks** is a real crate that always holds
everything.

**On screen / keyboard:** **Enter** on a row, then **C**, to add it to a crate.
The picker lists your crates — type to filter, **Enter** adds to the highlighted
crate or creates one with the name you typed, **Esc** closes.

To open a crate: **Space**, type its name, **Enter** on it under 📦 CRATES.

**Esc** clears an active crate or tag filter and puts the whole library back.

## Analysis

Sholto analyses each track once and remembers it, so it's instant every time
after. A track goes through tempo and beats, then key, then stems (drums, vocals,
bass, other), then the song sections.

- A small moving bar in **ANALYZED** means something is running for that row.
- A tick means tempo, beats **and** stems are all done.
- Stems need **demucs** installed; song sections use a simpler built-in guess.

**Re-analyse a track** — for the odd one whose beats came out wrong:

**On screen:** double-click the row.
**Controller:** hold the browse knob on it for about a second.

That recomputes tempo, beats and key from scratch and overwrites what was
stored.

## BPM and key

The **BPM** column shows the detected tempo with your ½ / ×2 correction applied
— see [correcting the tempo](deck.md#beatgrid) on the deck page. The correction
is remembered per track.

The **KEY** column is the Camelot code. Its colour follows the usual DJ
convention, so keys that sit next to each other on the wheel look alike. Once a
deck has a key loaded, every chip in the library that mixes harmonically with it
gains a white outline.
