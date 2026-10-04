# Sholto — project guide for Claude

Sholto is a 2-deck DJ application: .NET 10 + AvaloniaUI 11, targeting the Pioneer
DDJ-FLX4 controller, running on Linux (PipeWire/OSS audio). Local library, offline
analysis, no cloud.

## Repository layout

No `.sln` — build the app project directly; project references pull in the rest.

| Project | Role |
|---|---|
| `src/Sholto.Interface.MainUI` | Avalonia UI: `Views/` (`.axaml` + `.axaml.cs`), `ViewModels/`, custom-drawn `Controls/`. Entry point. |
| `src/Sholto.App.Audio` | Decoding + playback. NAudio/NLayer/SoundFlow. `AudioFileDecoder.TargetSampleRate = 48000`. Also owns per-deck track-lifetime state such as `TrackAnalysis`, the results bag a deck holds for the life of a loaded track; `TrackAnalysis` fires per-type events (see below). |
| `src/Sholto.App.Analysis` | Analysis domain + orchestration — the analysers that fill `TrackAnalysis`. |
| `src/Sholto.App.Storage` | EF Core + SQLite persistence, caches, crates, markers, tags. |
| `src/Sholto.Interface.Controller` | DDJ-FLX4 HID/MIDI mapping. |
| `src/Sholto.App.Library` | Library/file metadata (z440.atl.core tag reading). |
| `src/Sholto.App.Dsp` | Shared DSP primitives: crossover frequencies, crossfade curve. |
| `src/Sholto.App.ExternalTools` | Boundary to madmom/demucs/ffmpeg subprocesses. |
| `src/Sholto.Interface.Faceplate` | On-screen controller guide; joined to `Sholto.Interface.Controller` by gesture name only. |
| `tools/Sholto.Interface.Bench` | Dev-only headless interface (no Avalonia, no MainUI): renders decks to WAV, measures it, runs scripted scan/gesture/midi scenarios as Commands on the bus (`render`, `measure`, `state`, `headless`, `latency`). |
| `tools/Sholto.Interface.MainUI.Harness` | Dev-only MainUI test harness: drives the real window with no screen via Avalonia.Headless (`ui`, `screenshot`). Run: `dotnet run --project tools/Sholto.Interface.MainUI.Harness -- ui --scenario x.json`. |

## Build / run / test

```bash
cd /home/s/projects/open-dj
dotnet build src/Sholto.Interface.MainUI/Sholto.Interface.MainUI.csproj -nologo      # build (also builds refs)
dotnet run   --project src/Sholto.Interface.MainUI/Sholto.Interface.MainUI.csproj    # build + run
dotnet run   --project src/Sholto.Interface.MainUI/Sholto.Interface.MainUI.csproj --no-build   # run last build
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
   use `pkill -f Sholto.Interface.MainUI`, it self-matches the launch command and exits 144.)
2. **Verify the dll timestamp advanced after every build:**
   ```bash
   stat -c '%y' src/Sholto.Interface.MainUI/bin/Debug/net10.0/Sholto.dll
   ```
   If the timestamp did not move, the build was a no-op — rebuild (run `dotnet build`
   on its own, not buried in a `;`-chain), and if still stale, `touch` the changed
   file or `dotnet build --no-incremental`.
3. Only then `dotnet run --no-build`.

Do not conclude anything from a screenshot until you've confirmed the dll is fresh.

### Killing instances safely

```bash
pkill -9 -f "Sholto.dll"        # matches the running app, NOT the build/run command
pgrep -af "Sholto.dll"          # confirm none remain (ignore the pgrep line itself)
```
`ps` will still show `MSBuild.dll` / `VBCSCompiler` daemons — those are the build
server, leave them. `pkill -f "Sholto.Interface.MainUI"` (no `.dll`) matches `dotnet run …Sholto.Interface.MainUI…`
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

`Sholto.Audio/TrackAnalysis.cs` fires a **typed event per analysis stage**, then a
generic `AnyReady`. UI ViewModels subscribe to only the events they depend on and
re-notify just the affected bindings (cause→effect is explicit):

`BasicReady` (peaks, BPM, downbeats) · `KeyReady` · `StemsReady` / `StemPeaksReady`
(Demucs) · `VocalRegionsReady` · `SongSegmentsReady` (song structure).

Song sections: `SongSegmentAnalyzer` produces the section labels on `BasicReady`
(energy envelope + beatgrid → intro/build/drop/…) — an instant heuristic, and
currently the only song-section source (see `TODO.md` for a removed optional
model-based analyser that used to replace it via `SongSegmentsReady`).

External analyzers are subprocesses expected on `PATH`: madmom-onnx (beats/downbeats),
Demucs (stems). Absence degrades gracefully.

## Storage gotchas (`Sholto.Storage`)

- **Guids are stored lowercase TEXT.** `LowercaseGuidConverter` + matching
  `ConfigureConventions` — a case mismatch silently breaks lookups (this caused
  "analysis lost between restarts").
- **WAL + busy_timeout** via `SqlitePragmaInterceptor`; do **not** use `Cache=Shared`
  (it reintroduced lock contention). Concurrent analysis writers were verified 20/20.
- DB lives at `~/.local/share/sholto/library.db`. Migrations auto-apply on startup;
  The pre-EF raw-SQL schema and its one-shot migration were removed 2026-09-11:
  no released build ever wrote one (EF was present in the first commit).

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

## Code conventions

- No statics except extension methods and what the language or a framework forces (`Main`, `const`, Avalonia property registrations, EF migrations). Collaborators come in through constructors; anything built once comes from an injected factory instance, built at a composition root. Call sites never pass raw recipe arguments; a factory names the recipe (e.g. `BeatgridFactory.None()`).
- Prefer primary constructors: `public sealed class TagService(IDbContextFactory<SholtoDbContext> factory, TagNameNormalizer normalizer)` with `private readonly IDbContextFactory<SholtoDbContext> _factory = factory;`. Members use the `_field`, never the bare parameter (avoids the CS9124 capture-plus-store shape).
- Do not use a primary constructor for: constructors with logic beyond assignment, classes with several constructors, private constructors, XAML-created controls, EF `DbContext`/converters.
- One top-level type per file, named for the type.
- Analyzers vs analysis stages: an analyzer (`I*Analyzer`, extends the empty marker `IAnalyzer`, in `Sholto.Analysis/Analyzers/<Thing>/`) computes one result itself. An analysis stage (`*AnalysisStage`, extends the empty marker `IAnalysisStage`, in `Sholto.Analysis/Stages/`) is a composite that runs several analyzers and external-tool steps in a fixed order — e.g. `BasicAnalysisStage` (beats + waveform peaks + beatgrid), `StemAnalysisStage` (demucs + stem peaks + vocal regions). A composite of analyzers is always named `...AnalysisStage`, never `...Analyzer`. `IAnalysisStep` is different again: one external-tool process (madmom, demucs).

### Architecture: Interface / Data / App

Target; migration in progress.

- Three layers by package name: `Sholto.Interface.*` (MainUI — the exe and composition root, Controller(+Mappings), Faceplate(+Devices), Keyboard, Bench) · `Sholto.Data` (the bus) · `Sholto.App.*` (headless core; no Avalonia).
- Dependency rule: `Interface.*` → `Data` only; `App.*` → `Data` + its own sub-projects; `Data` → nothing. Only MainUI's composition root references everything.
- `Sholto.Data` carries exactly three message kinds, all strongly typed `readonly record struct`s: Commands in ("do this", no result); Queries in ("I want to know", typed result to the caller, never changes state); Events out ("this thing happened"; interfaces subscribe opt-in; state events replay their last value to late subscribers, facts don't).
- Gestures exist only inside an interface: its mapping from buttons/keys (or on-screen buttons) to the Command intended. Modifiers like Shift and platter-touch stay inside the interface.
- The App is the only source of truth for domain state (incl. cue, master-cue, pad page, Inspect mode). Interfaces may keep transient caches built from Events. Presentation state (overlay open, typed text, hover) stays in MainUI.
- Feedback translation (events → LEDs/MIDI) lives in each interface's output side.
- Threading: one app thread = the UI thread, lent to the App via `IAppThread` and `IFrameClock` (defined in `Sholto.Data`, implemented by MainUI over Avalonia's dispatcher/timer). App code assumes a single thread and takes no locks; each interface marshals its own input onto the app thread; the audio callback stays on its own thread; Bench uses an immediate `IAppThread` and a manual clock.

### Colours and theming

- No colour literals in code or XAML: no `"#RRGGBB"`/`"#AARRGGBB"` strings, no `Color.Parse`, no `Color.FromRgb/FromArgb` with literal values, no hex brushes in `.axaml`. Every colour comes from the theme.
- Themes are JSON: bundled in `src/Sholto.Interface.MainUI/Themes/*.json` (AvaloniaResource), user themes in `~/.config/sholto/themes/`. `SholtoThemeJson` parses them into `SholtoTheme`; the schema is documented in its doc comment — update that comment when adding a key.
- Lookup order for a colour: the theme's JSON → the bundled `Themes/defaults.json` (non-derivable defaults) → derivation from the theme's core colours (`WaveformPaletteFactory`, `MinimapPaletteFactory`). New keys are optional and get their default in `defaults.json`, so existing user themes keep working; don't add values to every bundled theme.
- Named waveform presets live in the fixed catalogue `Themes/waveform-presets.json`; themes refer to a preset by name.
- In XAML use `{DynamicResource Sholto…}` brushes published from the current theme (MainWindow applies the theme to `Application.Resources`). In controls, take colours from the bound palette (`Palette` StyledProperty); a control created from XAML draws nothing until its palette arrives — no hardcoded fallback palette.
- Alpha variations of a theme colour are applied in code (`WithAlpha`), not stored as separate keys.
- Exception: drawings of physical hardware (e.g. `DdjFlx4Layout.axaml`, the Pioneer controller) are not theme data.

## Git conventions

- Remote `origin` = `git@github-freedomfirst26:freedomfirst26/Sholto.git` (SSH host
  alias `github-freedomfirst26`); commit identity `freedomfirst26`.
- **Do NOT add a Claude co-author trailer to commits.**
- **Do NOT `git push`** unless explicitly told to in the current turn. Commit only when
  asked.

## Keep the README in sync

`README.md` is the user-facing feature list and dependency list, written for a
**non-technical Linux user**. Treat it as part of the change, not an afterthought:

- **Added or changed a user-facing feature?** Add/adjust its bullet in the right
  "What it does" subsection (and the **Keyboard** / **Your DDJ-FLX4** lists if it
  has a shortcut or control), in plain language a layman understands.
- **Added a new external dependency** — a system package, a CLI tool the app shells
  out to, or a runtime the user must install? Add it to the README's
  **What it needs** list, marked **required** or **optional** (state what's lost
  without it), *and* to `install.sh`.

Do it in the same commit that adds the feature/dependency. Don't overclaim —
only list formats/features that actually work (e.g. the decoder strategies in
`Sholto.Audio` define which audio formats are really supported).

## Working docs

Living plan/spec for this app: `~/.claude/plans/sholto.md` (single file — append, don't
create new dated files). Capture substantive findings there as they surface.

## Branch docs

This project keeps one markdown document per branch, at
`~/.claude/branches/<project>-<branch with "/" replaced by "-">.md`, in Claude's home
folder — not inside the repository — created when the branch is created. It lives outside
the repo because it is working state, not a project artifact: keeping it out keeps `git
status` and review diffs clean instead of polluting them. It has four required sections,
in order:

1. **Gist** — what the branch changes, why, and what is explicitly out of scope.
2. **Acceptance criteria** — a checklist, where each item is checkable by someone else
   rather than a judgement call.
3. **Findings** — what was learned doing the work, with file:line and numbers, written
   so each finding stands alone without the surrounding conversation.
4. **Open / not done** — what remains, and what is blocked on Sebastian.

Associated artifacts — proof WAVs, before/after measurements, profiling output — are
listed in the doc with their paths, so the evidence for a claim stays findable.

This is distinct from `~/.claude/plans/sholto.md`, the long-running plan spanning branches,
and from `TODO.md`, the product/feature backlog.

Branch names follow `<type>/<short-kebab-topic>`, using the same type prefixes as
commit subjects: `feat`, `fix`, `refactor`, `docs`.

## Releases and the changelog

A release is a `v*` tag; it contains every commit since the previous tag. Notes
for it are hand-written in `CHANGELOG.md` (Keep a Changelog style), not
auto-generated from commits.

**Every user-visible change updates `CHANGELOG.md` in the same change**, under
`## Unreleased`, in one of `### New`, `### Improved`, `### Fixed`,
`### Housekeeping`. Write for a DJ, not a developer: what changed at the decks,
one or two lines, no class or file names. Same rule as the README: it is part of
the change, not an afterthought. Internal-only refactors don't get a line.

**Cutting a release** (the user runs these; never tag or push on their behalf):

1. Rename `## Unreleased` to `## vX.Y.Z` in `CHANGELOG.md` and add a fresh empty
   `## Unreleased` above it. Commit.
2. `git tag -a vX.Y.Z -m "vX.Y.Z"` then `git push origin main vX.Y.Z`.
3. `.github/workflows/release.yml` fires on the tag: it publishes a
   self-contained linux-x64 single-file build, bundles README + LICENSE +
   install-deps.sh into `sholto-vX.Y.Z-linux-x64.tar.gz`, extracts the
   `## vX.Y.Z` section from `CHANGELOG.md` as the release body, and creates the
   GitHub Release with the tarball attached. If the changelog section is
   missing it warns and falls back to GitHub's auto-generated commit list.

Versioning: pre-1.0 semver. Bump the minor for new features, the patch for a
fixes-only release. Nothing else in the repo carries the version — the tag is
the source of truth.
