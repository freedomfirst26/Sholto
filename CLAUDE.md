# Sholto — project guide for Claude

Sholto is a 2-deck DJ application: .NET 10 + AvaloniaUI 11, targeting the Pioneer
DDJ-FLX4 controller, running on Linux (PipeWire/OSS audio). Local library, offline
analysis, no cloud.

## Repository layout

No `.sln` — build the app project directly; project references pull in the rest.

| Project | Role |
|---|---|
| `src/Sholto.App` | Avalonia UI: `Views/` (`.axaml` + `.axaml.cs`), `ViewModels/`, custom-drawn `Controls/`. Entry point. |
| `src/Sholto.Audio` | Decoding + playback. NAudio/NLayer/SoundFlow. `AudioFileDecoder.TargetSampleRate = 48000`. |
| `src/Sholto.Analysis` | Analysis domain + orchestration. `TrackAnalysis` fires per-type events (see below). |
| `src/Sholto.Storage` | EF Core + SQLite persistence, caches, crates, markers, tags. |
| `src/Sholto.Controller` | DDJ-FLX4 HID/MIDI mapping. |
| `src/Sholto.Music` | Library/file metadata (z440.atl.core tag reading). |

## Build / run / test

```bash
cd /home/s/projects/open-dj
dotnet build src/Sholto.App/Sholto.App.csproj -nologo      # build (also builds refs)
dotnet run   --project src/Sholto.App/Sholto.App.csproj    # build + run
dotnet run   --project src/Sholto.App/Sholto.App.csproj --no-build   # run last build
```

### ⚠️ The dll-not-rebuilding trap (read this before trusting a screenshot)

Symptom seen repeatedly: `dotnet build` prints **"Build succeeded"** but the compiled
dll is **not actually updated**, so the running app shows *old* behaviour. Whole
debugging sessions have been wasted testing a stale binary — especially with
`--no-build`, and especially for `.axaml` changes (Avalonia bakes XAML into the
assembly at compile time).

The main cause: **an app instance was still running / holding the dll when you built.**

**Always do this:**

1. **Kill every running instance before building.** (See kill recipe below — do NOT
   use `pkill -f Sholto.App`, it self-matches the launch command and exits 144.)
2. **Verify the dll timestamp advanced after every build:**
   ```bash
   stat -c '%y' src/Sholto.App/bin/Debug/net10.0/Sholto.App.dll
   ```
   If the timestamp did not move, the build was a no-op — rebuild (run `dotnet build`
   on its own, not buried in a `;`-chain), and if still stale, `touch` the changed
   file or `dotnet build --no-incremental`.
3. Only then `dotnet run --no-build`.

Do not conclude anything from a screenshot until you've confirmed the dll is fresh.

### Killing instances safely

```bash
pkill -9 -f "Sholto.App.dll"        # matches the running app, NOT the build/run command
pgrep -af "Sholto.App.dll"          # confirm none remain (ignore the pgrep line itself)
```
`ps` will still show `MSBuild.dll` / `VBCSCompiler` daemons — those are the build
server, leave them. `pkill -f "Sholto.App"` (no `.dll`) matches `dotnet run …Sholto.App…`
and kills the launcher / returns exit 144 — avoid it.

## Seeing the app on screen (headless verification)

The app window is titled **`Sholto`** and often sits on a **second monitor** (x offset
> 1920). `gnome-screenshot -w` captures the *focused* window, which is usually the
terminal — unreliable. Capture the app **by its geometry** instead:

```bash
IFS=, read x y w h <<< "$(wmctrl -lG | awk '/[[:space:]]Sholto$/{print $3","$4","$5","$6}')"
ffmpeg -y -f x11grab -video_size ${w}x${h} -i :0.0+${x},${y} -frames:v 1 shot.png -loglevel error
```
Then Read `shot.png`. (`ffmpeg` is the only image tool installed — no ImageMagick/scrot.)
X11, `DISPLAY=:0`. To bring the app forward: `wmctrl -i -a <id>` (id from `wmctrl -l`).

## Analysis pipeline (event-driven)

`Sholto.Analysis/TrackAnalysis.cs` fires a **typed event per analysis stage**, then a
generic `AnyReady`. UI ViewModels subscribe to only the events they depend on and
re-notify just the affected bindings (cause→effect is explicit):

`BasicReady` (peaks, BPM, downbeats) · `KeyReady` · `StemsReady` / `StemPeaksReady`
(Demucs) · `VocalRegionsReady` · `SongSegmentsReady` (song structure).

Song sections: `SongSegmentAnalyzer` produces an **instant heuristic** on `BasicReady`
(energy envelope + beatgrid → intro/build/drop/…); `AllInOneSegmentAnalyzer` (the
`allin1` CLI, optional/heavy) later **replaces** it via `SongSegmentsReady` with real
labels. `allin1` is best-in-class for functional labelled sections; the heuristic is
the always-available fallback.

External analyzers are subprocesses expected on `PATH`: madmom-onnx (beats/downbeats),
Demucs (stems), allin1 (structure). Absence degrades gracefully.

## Storage gotchas (`Sholto.Storage`)

- **Guids are stored lowercase TEXT.** `LowercaseGuidConverter` + matching
  `ConfigureConventions` — a case mismatch silently breaks lookups (this caused
  "analysis lost between restarts").
- **WAL + busy_timeout** via `SqlitePragmaInterceptor`; do **not** use `Cache=Shared`
  (it reintroduced lock contention). Concurrent analysis writers were verified 20/20.
- DB lives at `~/.local/share/sholto/library.db`. Migrations auto-apply on startup;
  incompatible schema → `IncompatibleSchemaException`.

## UI / rendering notes

- Avalonia is **single-UI-thread**; heavy visuals use a background-compute → post
  pattern (peaks, EQ powers computed off-thread, drawn on the render thread).
- Custom `Control`s draw in `Render(DrawingContext)` with `MeasureOverride` for size;
  invalidate via `AffectsRender<T>(prop)` or `InvalidateVisual()`. Prefer plain
  `DrawingContext` primitives over `ICustomDrawOperation`/Skia leases — the Skia custom
  op has repeatedly failed to composite in nested layouts.
- **Grid does not clip children to their cell**, and z-order = declaration order — a
  later sibling paints over an earlier one even across rows. Watch for overlap when a
  control "renders" (bounds valid, `IsEffectivelyVisible` true) but isn't visible.
- Theme colours come through `{DynamicResource Sholto…}` (see `MainWindow.axaml`
  resources), swapped at runtime on theme change. Use DynamicResource, not `$parent`
  traversal (goes stale under Fluent hover/menu state).
- Runtime layout tree is inspectable with DevTools (F12) via `AvaloniaUI.DiagnosticsSupport`.

## Runtime environment

- Music library lives on an NTFS drive that is not always mounted. If logs say
  `music dir not reachable: /media/s/Data/Music/`, mount without sudo:
  ```bash
  udisksctl mount -b /dev/sda2
  ```
- Load a track via keys `1`/`2` (send selected → deck) or the FLX4 LOAD button.
  Double-clicking a track re-runs analysis.

## Git conventions

- Remote `origin` = `git@github-freedomfirst26:freedomfirst26/Sholto.git` (SSH host
  alias `github-freedomfirst26`); commit identity `freedomfirst26`.
- **Do NOT add a Claude co-author trailer to commits.**
- **Do NOT `git push`** unless explicitly told to in the current turn. Commit only when
  asked.

## Working docs

Living plan/spec for this app: `~/Projects/sholto.md` (single file — append, don't
create new dated files). Capture substantive findings there as they surface.
