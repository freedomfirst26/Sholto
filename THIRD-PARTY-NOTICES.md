# Third-party notices

Sholto itself is licensed under the [Business Source License 1.1](LICENSE).
It depends on the software below, each under its own license. Nothing here is
redistributed by Sholto unless the "Shipped" column says so — the external
tools are installed separately by `install-deps.sh` and invoked as processes.

## ⚠ Non-commercial dependency — read before commercial use

**madmom** (beat and downbeat detection) is on Sholto's **required** path:
`Sholto.Analysis/BasicAnalysis.cs` runs it on every track load, and without a
beatgrid a track will not play.

madmom's licensing is split:

- **Source code** — BSD 2-clause. Copyright (c) 2012-2014 Department of
  Computational Perception, Johannes Kepler University, Linz, Austria and
  Austrian Research Institute for Artificial Intelligence (OFAI), Vienna.
- **Model / data files** — **Creative Commons BY-NC-SA 4.0**, i.e. *non-commercial
  only*. madmom's own LICENSE adds: "If you want to include any of these files
  (or a variation or modification thereof) or technology which utilises them in
  a commercial product, please contact Gerhard Widmer at
  gerhard.widmer@jku.at."

**Consequence:** a commercial license to Sholto does **not** carry a right to use
madmom's models. Organizations must obtain their own permission from JKU, or
wait for Sholto to ship a permissively-licensed default beat tracker. If you are
buying a commercial license, raise this first: <freedomfirst26@proton.me>.

## External tools (installed separately, invoked as processes)

| Tool | Role | Required? | License |
|---|---|---|---|
| madmom / madmom-onnx | beat + downbeat detection | **required** | BSD 2-clause (code) + **CC BY-NC-SA 4.0 (models)** — see above |
| ffmpeg | fallback audio decoding (`FfmpegDecodeStrategy`) | fallback only | LGPL-2.1+, or GPL-2.0+ for `--enable-gpl` builds (most distro builds). Invoked as a separate process; not linked, not shipped. |
| demucs | stem separation | optional | MIT (Meta) |

## Bundled libraries (NuGet, redistributed with Sholto)

| Package | Role | License |
|---|---|---|
| Avalonia (+ Desktop, Skia, Themes.Fluent, Fonts.Inter) | UI toolkit | MIT |
| AvaloniaUI.DiagnosticsSupport | debug-only diagnostics | MIT |
| SoundFlow | audio engine (miniaudio) | MIT |
| NAudio | WAV / MP3 decoding | MIT |
| NLayer.NAudioSupport | MP3 decoding | MIT |
| z440.atl.core | audio tag reading | MIT |
| CommunityToolkit.Mvvm | MVVM helpers | MIT |
| Microsoft.Extensions.Options | configuration | MIT |
| Microsoft.Data.Sqlite | storage | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | storage | MIT |

All of the above are MIT and impose no restriction on Sholto's commercial track.

## Keeping this file honest

Any PR that adds a dependency must add it here with its license — see
[CONTRIBUTING.md](CONTRIBUTING.md).
