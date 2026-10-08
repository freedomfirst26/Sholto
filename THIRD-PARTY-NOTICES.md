# Third-party notices

Sholto itself is licensed under the [PolyForm Shield License 1.0.0](LICENSE) from v1.1.0 (v0.1.0 and v0.2.0 are under PolyForm Noncommercial 1.0.0 with a free grant for individuals; v0.3.0 and v1.0.0 are under the Business Source License 1.1).
It depends on the software below, each under its own license. Nothing here is
redistributed by Sholto unless the "Shipped" column says so — the external
tools are installed separately by `install-deps.sh` and invoked as processes.

## ⚠ Non-commercial dependency — read before commercial use

**madmom** (beat and downbeat detection) is on Sholto's **required** path:
`src/Sholto.App.ExternalTools/MadmomBeatAnalysisStep.cs` runs it on every track load, and without a
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

**Consequence:** Sholto's licence, free or commercial, does **not** carry a right to
use madmom's models. Whether using Sholto at a paid gig counts as commercial use of
them is a question for JKU, not for us. Anyone using Sholto commercially (paid gigs
included) or buying a commercial license should get their own permission from JKU, or
wait for Sholto to ship a permissively-licensed default beat tracker. Questions:
<freedomfirst26@proton.me>.

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
| Microsoft.Extensions.Options | configuration | MIT |
| Microsoft.Data.Sqlite | storage | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | storage | MIT |

All of the above are MIT and impose no restriction on Sholto's commercial track.

## Keeping this file honest

Any PR that adds a dependency must add it here with its license — see
[CONTRIBUTING.md](https://github.com/freedomfirst26/Sholto/blob/main/CONTRIBUTING.md).
