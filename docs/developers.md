# For developers

Building, reading or changing the source. If you only want to play, start at
[install.md](install.md) instead.

- **Build from source:** see [install.md](install.md#option-3--build-from-source).
- **Contributing** (licensing of contributions, the DCO, third-party code,
  trademark): [CONTRIBUTING.md](../CONTRIBUTING.md).
- **What changed in each release:** [CHANGELOG.md](../CHANGELOG.md).
- **Third-party licences:** [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
- **Writing your own theme** (the JSON format): [themes.md](themes.md#writing-your-own).

## How the code is laid out

Only useful if you're reading or building the source. Sholto is one app split into
ten projects, and the split is deliberate: the projects at the bottom of the list
know nothing about the ones above them, so you can change the look of the app without
touching how it sounds, and change how it sounds without touching what it knows about
a track.

The direction dependencies run — each project only sees the ones to its right:

```
Sholto.App  ->  Analysis, Audio, Controller, ExternalTools, Faceplate, Library, Storage
Sholto.Audio      ->  Analysis, Dsp
Sholto.Storage    ->  Analysis
Sholto.ExternalTools -> Analysis
Sholto.Analysis   ->  Dsp
Sholto.Faceplate  ->  Controller
Sholto.Dsp, Sholto.Controller, Sholto.Library  ->  nothing
```

| Project | What lives there |
|---|---|
| `src/Sholto.Interface.MainUI` | Everything you see, and the one place everything is wired together. Views and view models, the custom-drawn waveform and minimap, themes, keyboard handling, and the routing that turns a gesture into a deck action. It is the only composition root — every other project is constructed here and handed its collaborators; none of them reach for each other. No audio processing and no analysis maths lives here. |
| `src/Sholto.App.Audio` | Everything you hear. The decks, the mixer, EQ, filter, echo and beat-repeat, loops, tempo and pitch, stem muting, headphone cue routing, and the file decoders that turn mp3/FLAC/WAV/AIFF/M4A into 48 kHz stereo samples. It owns the audio engine and the audio thread, and knows nothing about windows, view models or themes. |
| `src/Sholto.App.Analysis` | What a track *is*: waveform peaks, beatgrid and BPM, musical key, song sections, vocal regions, stem descriptions — plus the interfaces through which those results get cached, stored and computed. Pure computation and plain types. It never plays audio, never draws, and never opens the database itself. |
| `src/Sholto.App.Storage` | The database. SQLite and EF Core: the schema, its migrations, and the tables behind analysed tracks, crates, tags, markers, BPM and grid overrides, and settings. This is the disk — the saved copy of everything — not the music collection. |
| `src/Sholto.App.Library` | The music collection on your filesystem. Walking your music folder, reading the artist and title tags out of each file, and free-text search across the result. It never touches the database; a scan produces tracks, and the App decides what to persist. |
| `src/Sholto.Interface.Controller` | The physical controller. MIDI in and out over ALSA, the DDJ-FLX4 button and knob mapping, the LEDs, and the recognizer that turns a raw press into a named gesture like "hold shift and turn the jog". The rest of the app talks to it in gesture names and never sees a note number. |
| `src/Sholto.Interface.Faceplate` | The controller guide drawn on screen — the picture of the device, the chips, and the JSON that describes what every control does. It describes; it never decides. Pressing something on the real unit is resolved by `Sholto.Interface.Controller`, and the two are joined only by the gesture's name. |
| `src/Sholto.App.Dsp` | Signal-processing primitives shared by analysis and playback: the low/mid/high crossover frequencies, and the constant-power crossfade curve used when mixing between decks. The crossover frequencies live here so the colours on the waveform can never drift away from what the EQ knobs actually cut. |
| `src/Sholto.App.ExternalTools` | The arm's-length boundary to the separate programs — finding madmom, demucs and ffmpeg on your PATH, starting them, reading their output, following their progress, and reporting what came back. It is the only place in Sholto that starts another process. |
| `tools/Sholto.Interface.Bench` | **A development tool, not part of the app you install.** It renders the decks to a WAV file with no sound card and scripts a controller with no window, so things you could previously only judge by ear — audio dropping out, a backspin jolting — become numbers a test can check. |
| `tools/Sholto.Interface.MainUI.Harness` | **A development tool, not part of the app you install.** It drives the real window with no screen (keys, clicks, screenshots), so things you could previously only judge by eye become something a test can check. |

The line between `Sholto.App` and `Sholto.Audio` is the one worth remembering: if it
makes a sound, it belongs in `Sholto.Audio`; if it shows you something, responds to you,
or decides which objects get built and plugged into each other, it belongs in
`Sholto.App`.


## Documentation screenshots

The pictures in the README and these docs are real renders of the app, not mock-ups:
the MainUI harness drives the real window headlessly over a silent demo library, gives
the decks a made-up analysis (the harness runs no analysers), and captures at 2× or 3×.
A scenario can press keys (`key`, with `shifted` and `ctrl`), type text (`type`), let time pass (`wait`), click and capture.
To take them again — after a theme or layout change, say:

```bash
bash tools/Sholto.Interface.MainUI.Harness/DocScreenshots/capture.sh
```

It needs ffmpeg and python3 with Pillow, writes `pictures/*.webp`, and leaves the raw
captures in a temporary folder. Look at every image before committing it.
