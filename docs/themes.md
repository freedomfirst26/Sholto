# Themes

## Picking one

**Settings → Theme.** Each entry shows its name and four little colour chips —
background, primary, accent and mint — so you can tell them apart at a glance.
Click one and the whole app changes immediately. Your choice is remembered.

Eleven themes ship with Sholto: Classic, Serato, Front Line Assembly, Silence
Groove, Jeremy Soule, Type O Negative, Birthday Massacre, Pantera, Dimmu
Borgir, Aphex Twin, and The Prodigy.

## What a theme controls

- The app itself — backgrounds, borders, text, the accent used for the analysed
  tick and the BPM column.
- The key chips — the whole Camelot colour wheel is re-toned from the theme, in
  the library and on the decks.
- The section map — the strip above each waveform: its backdrop, its playhead,
  the dividers, and a colour per section type (intro, build, drop, and so on).
- The waveform — the bass and mid bands, the downbeat lines, the beat ticks, the
  playhead, markers, the gain line, and the loop band.

## What a theme does *not* control

Three things stay the same on every theme, on purpose:

- **The vocal marks** on the waveform are always green (grey when VOX is muted).
- **The white core** of the waveform — the innermost band — so the vocal marks
  stay readable on top of it.
- **The VOX chip** under the disc, which matches that same green.

## Writing your own

Themes are small JSON files. Drop yours in:

```
~/.config/sholto/themes/
```

(or `$XDG_CONFIG_HOME/sholto/themes/` if you've set that). Sholto picks up every
`.json` file in there at startup and adds it to the menu alongside the built-in
ones — no rebuild. The quickest start is to copy a bundled theme out of
`src/Sholto.Interface.MainUI/Themes/`, rename it, and change the colours.

Every colour is `#RRGGBB`, or `#AARRGGBB` when it needs transparency.

**The required part** — a name, ten app colours, the fade colour used over the
played half of the waveform, a waveform preset name, and the five numbers that
generate the Camelot key chips:

```json
{
  "name": "My Theme",
  "bgDeep": "#111111",
  "surface": "#1A1A1A",
  "surfaceRaised": "#222222",
  "border": "#333333",
  "primary": "#00FFCC",
  "accent": "#FFC700",
  "accentBg": "#33FFC700",
  "mint": "#FFFFFF",
  "textBright": "#EEEEEE",
  "textMuted": "#888888",
  "playedFadeColor": "#111111",
  "waveformPalette": "Bands",
  "camelotPalette": {
    "hueOffset": 0,
    "saturation": 0.78,
    "majorLightness": 0.55,
    "minorLightness": 0.42,
    "onChipForeground": "#101820"
  }
}
```

What those mean, in plain words:

| Key | What it colours |
|---|---|
| `bgDeep` | The window behind everything |
| `surface`, `surfaceRaised` | The track list, and the bars and menus above it |
| `border` | Hairlines and the rings on the disc |
| `primary` | The main highlight colour |
| `accent`, `accentBg` | The analysed tick, the BPM column, the selected row |
| `mint` | The needle on the disc, and the default playhead |
| `textBright`, `textMuted` | Titles, and everything secondary |
| `playedFadeColor` | The fade over the part of the waveform already played |
| `waveformPalette` | A preset name that seeds the downbeat colour — `"Bands"` is the standard three-band look |
| `camelotPalette` | Rotates and tones the key-chip colour wheel; `onChipForeground` is the text on those chips |

**Two optional sections.** Leave either out entirely, or leave out any single
key inside it, and Sholto works the colour out from your accent, primary and
mint — so a short theme file is a perfectly good theme file.

`"minimap"` colours the section-map strip: `backdrop`, `playhead`, `label`,
`divider`, and one colour each for `intro`, `buildUp`, `drop`, `breakdown`,
`verse`, `chorus`, `bridge` and `outro`.

`"waveform"` colours the big waveform: `background`, `low` (the bass band) and
`mid`, plus `downbeat`, `beatTick`, `playhead`, `marker`, `gain` (the fader
line) and `loop` (the loop band). There is no `high` — the inner band is always
white.

One rule worth keeping when you pick band colours: no band may be so dark it
disappears against the background, and neighbouring bands must differ in hue or
brightness. Otherwise the waveform reads as one flat blob and you can't spot the
arrangement.

If a theme file has a mistake in it, Sholto skips that file and prints a line to
the console rather than refusing to start.
