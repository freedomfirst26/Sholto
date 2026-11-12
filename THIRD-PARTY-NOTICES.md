# Third-party notices

Sholto itself is licensed under the [PolyForm Shield License 1.0.0](LICENSE) from v1.1.0 (v0.1.0 and v0.2.0 are under PolyForm Noncommercial 1.0.0 with a free grant for individuals; v0.3.0 and v1.0.0 are under the Business Source License 1.1).
It depends on the software below, each under its own license. Nothing here is
redistributed by Sholto unless the "Shipped" column says so — the external
tools are installed separately by `install-deps.sh` and invoked as processes.

## Beat detection: Beat This! (required) and madmom (optional fallback)

**Beat This!** (beat and downbeat detection, CPJKU, JKU Linz) is Sholto's
**required** beat tracker: `src/Sholto.App.ExternalTools/BeatThisBeatAnalysisStep.cs`
runs its `beat_this` command on every track load, and without a beatgrid a track
will not play. Both its code and its model weights are permissive:

- **Code** — MIT. Its LICENSE: "MIT License — Copyright (c) 2024 Institute of
  Computational Perception, JKU Linz, Austria".
- **Model weights (checkpoints)** — MIT too. Its README, under *License*: "The code
  and the published model weights are released under the MIT license. Note that
  some of the training files are fully copyrighted or under limited Creative
  Commons licenses, and it is up to the user to assess whether this may impact
  their use case." The training files are not used or shipped by Sholto; the
  checkpoint (`final0`) is downloaded by Beat This! itself on its first run.

Sholto runs Beat This! with its default postprocessing, never its `--dbn` option
(which would pull madmom back in).

## ⚠ Non-commercial dependency — optional fallback only

**madmom** (beat and downbeat detection) is **no longer on Sholto's required path**
and is not installed by `install.sh` or `install-deps.sh`. Sholto uses it
(`src/Sholto.App.ExternalTools/MadmomBeatAnalysisStep.cs`) only as a fallback,
when Beat This! is not installed and madmom is.

madmom's licensing is split:

- **Source code** — BSD 2-clause. Copyright (c) 2012-2014 Department of
  Computational Perception, Johannes Kepler University, Linz, Austria and
  Austrian Research Institute for Artificial Intelligence (OFAI), Vienna.
- **Model / data files** — **Creative Commons BY-NC-SA 4.0**, i.e. *non-commercial
  only*. madmom's own LICENSE adds: "If you want to include any of these files
  (or a variation or modification thereof) or technology which utilises them in
  a commercial product, please contact Gerhard Widmer at
  gerhard.widmer@jku.at."

**Consequence:** if you install madmom yourself and leave Beat This! out, Sholto's
licence, free or commercial, does **not** carry a right to use madmom's models.
Whether using Sholto at a paid gig counts as commercial use of them is a question
for JKU, not for us. With Beat This! installed (the default), madmom is never run.
Questions: <freedomfirst26@proton.me>.

## External tools (installed separately, invoked as processes)

| Tool | Role | Required? | License |
|---|---|---|---|
| Beat This! (`beat-this`) | beat + downbeat detection | **required** | MIT (code and model weights) — see above |
| madmom / madmom-onnx | beat + downbeat detection, fallback when Beat This! is missing | optional, not installed by default | BSD 2-clause (code) + **CC BY-NC-SA 4.0 (models)** — see above |
| ffmpeg | fallback audio decoding (`FfmpegDecodeStrategy`) | fallback only | LGPL-2.1+, or GPL-2.0+ for `--enable-gpl` builds (most distro builds). Invoked as a separate process; not linked, not shipped. |
| demucs | stem separation | optional | MIT (Meta) |

Installed alongside Beat This!, in its own isolated environment (not shipped by
Sholto, only installed by the installers):

| Package | Role | License |
|---|---|---|
| PyTorch (`torch`) | runs the model | BSD-3-Clause (plus the permissive licences of its bundled components) |
| torchaudio | audio loading | BSD-2-Clause |
| torchcodec | mp3/m4a decoding for torchaudio | BSD-3-Clause |
| einops, rotary-embedding-torch | model building blocks | MIT |
| soxr (python-soxr) | resampling | LGPL-2.1-or-later |
| numpy | arrays | BSD-3-Clause |

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
