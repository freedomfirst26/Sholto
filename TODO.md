# TODO

- [ ] **Re-instate `LoopDebug`, if offline loop-seam capture is ever needed again.**
      Removed 2026-09-12: it was a diagnostic recorder that wrote the stem-mix
      output to a WAV file when `SHOLTO_LOOP_DEBUG_WAV` was set, so a "weird
      loop" passage could be captured and diffed sample-by-sample offline.
      `tools/Sholto.Bench`'s `render` subcommand now covers the same job better
      — deterministic and scriptable, no env var to remember, and it renders
      offline without needing a real audio device at all. `LoopDebug` was also
      the last remaining allocation on the audio thread (`new float[]` per
      buffer in `Append`), so removing it eliminates that too.

      How to bring it back:
      - `git show HEAD:src/Sholto.Audio/LoopDebug.cs` recovers the recorder
        itself — it was committed at HEAD before this removal.
      - `ILoopDebug.cs` (the port + `NullLoopDebug`) and `LoopDebugFactory.cs`
        (env-var lookup + wiring) were both created during this session and
        **never committed** — they will NOT come back from git. Their shape,
        for anyone rebuilding: a small `ILoopDebug` port (`Append(ReadOnlySpan<float>)`,
        `Close()`) with a shared no-op `NullLoopDebug.Instance`, and a
        `LoopDebugFactory.Create()` that reads `SHOLTO_LOOP_DEBUG_WAV` and hands
        back either a real recorder or the null object.
      - The approach worth keeping if reimplementing from scratch: a bounded
        queue fed from the audio thread (enqueue only, no I/O, no allocation
        once the buffer is pooled), a background writer thread that drains the
        queue and does the actual file I/O, writing a 32-bit float stereo WAV
        at `AudioFileDecoder.TargetSampleRate`.
      - Call sites that threaded `ILoopDebug` through (`Deck`, `DeckFactory`,
        `StemMixDataProvider`, `App.axaml.cs`'s composition root, and
        `tools/Sholto.Bench/Rendering/BenchDeckFactory.cs`'s `NullLoopDebug.Instance`
        argument) all had the parameter/field/call removed in the same change;
        follow the compiler again if reintroducing it.

- [ ] **Re-instate the optional `allin1` song-structure analyser, if wanted later.**
      Removed 2026-09-12 (KISS): it was the heaviest, most fragile external
      dependency — builds NATTEN from source, pulls madmom from a bare git URL
      with no pinned ref — was off by default, and had never actually been
      installed on this machine. The built-in heuristic segmenter
      (`SongSegmentAnalyzer`, driving the minimap from the energy envelope +
      beatgrid) is unaffected and remains the only section source.

      What it did: ran the `allin1` CLI (mir-aidj/all-in-one) on a track and
      produced real model-based intro/verse/chorus/bridge/outro labels, which
      replaced the heuristic's guesses via `TrackAnalysis.SongSegmentsReady` once
      they landed (the typed event itself is still in `TrackAnalysis.cs` — kept,
      since it costs nothing and nothing else needed removing there).

      How to bring it back:
      - `git show HEAD:src/Sholto.Analysis/AllInOneSegmentAnalyzer.cs` recovers
        the analyser (`ISongSegmentAnalyzer`/`ISongSegmentStatus` + the analyser
        itself were committed at HEAD before this removal).
      - `AllInOneTool.cs` (the `IToolDefinition<SongSegments>` adapter) was
        **never committed** — it will NOT come back from git. Its contract:
        ```csharp
        internal sealed class AllInOneTool(string jsonPath) : IToolDefinition<SongSegments>
        {
            public static string JsonPathIn(string workDir, string inputPath) =>
                Path.Combine(workDir, Path.GetFileNameWithoutExtension(inputPath) + ".json");

            public string Name => AllInOneSegmentAnalyzer.StepNameConst; // "segments"
            public string BinaryName => ExternalToolNames.AllIn1;        // "allin1"
            public bool IsRequired => false;

            public IReadOnlyList<string> BuildArgs(string input, string workDir) =>
                new[] { "-o", workDir, input };

            public ToolOutcome<SongSegments> Verify(string input, string workDir, string stdout, int exitCode)
            {
                // non-zero exit -> Failure; JSON missing at jsonPath -> Failure;
                // JSON present but unparseable -> Failure (distinct messages for each);
                // otherwise Parse(File.ReadAllText(jsonPath)) -> Success.
            }

            internal static SongSegments Parse(string json)
            {
                // reads root "segments" array; each item has start/end/label (doubles/string);
                // skips zero-length start==end markers; maps the Harmonix label set
                // (intro/start, outro/end, chorus, verse, bridge, break, inst/solo, else)
                // to SegmentKind via MapLabel.
            }
        }
        ```
      - Re-wire: `ExternalToolNames.AllIn1 = "allin1"` (+ back into `.All`), the
        `Deck` constructor's `ISongSegmentAnalyzer segmentAnalyzer` parameter and
        its `SegmentAnalyzer` property + the `SegmentAnalyzer.IsAvailable` call
        site around the stems kickoff in `Deck.Load`, `DeckViewModel`'s
        `OnSongSegmentsReady` subscription, `MainViewModel`'s constructor
        parameter, and the `allin1` composition + `ToolSet` cache-dir case in
        `App.axaml.cs`. Also restore the `install.sh`/`install-deps.sh` sections
        (`SHOLTO_INSTALL_ALLIN1=1` gate) and `sholto-deps.sh`'s `check_allin1` +
        its `deps_verify` line, and the README/THIRD-PARTY-NOTICES rows.

- [ ] **First-run tutorial.** The first time Sholto opens, walk the user through the app
      rather than leaving them to find it. It should introduce each part of the screen —
      the library, the decks, the waveforms, search, crates and tags — and open the
      **Faceplate at full size** as the controller chapter, so they meet the controller
      guide instead of discovering it by accident.
      Why it matters now: the README's how-to sections are being deleted because the
      Faceplate documents the controller better. But the Faceplate covers the CONTROLLER
      only. Until this tutorial exists, nothing teaches the keyboard shortcuts or the
      on-screen moves (clicking the BPM to open the tuner, double-clicking to re-analyse,
      pressing M to drop a marker). This is the thing that closes that gap.
      Backlog — not part of the Faceplate work.

- [ ] **A Faceplate for the keyboard.** The keyboard is an input device like the controller,
      and it deserves the same treatment: a picture of it, every bound key explained, and
      the key lighting up when you press it.
      The device contract already supports this — a device is a layout `.axaml` of shapes
      carrying `ControlSurface.Id`, plus a `.json` of descriptions keyed by gesture id, and
      no C# at all. See `src/Sholto.Faceplate/Devices/README.md`.
      **The one real piece of work** is that keyboard presses do not currently become
      gestures. Controller input flows MIDI -> `DdjFlx4Mapping` -> `ControllerEvent` ->
      `GestureRecognizer` -> `Gesture` -> `GestureBus`, but keyboard shortcuts are handled
      directly in `MainWindow.axaml.cs`'s key handler and never enter that pipeline. So this
      needs a keyboard gesture source feeding the same bus. Once it does, the guide, the
      live highlighting and the blink all work with no new UI code — and the keyboard
      shortcuts stop being duplicated between the key handler and the README.
      Backlog — not part of the current Faceplate work.

- [ ] **A shared modal shell for every overlay.** Sholto has five overlays — search, the tag
      editor, the track actions menu, the crate picker and now the Faceplate — and each one
      re-implements its own backdrop, its own click-outside-to-close, its own Esc handling
      and its own frame. They do not agree: some have a visible close button, some only
      answer to Esc, and the backdrop scrim differs between them.
      Build one `ModalShell` control that owns the backdrop, the frame, a visible X in the
      top right, Esc, click-outside, focus trapping and the open/close transition. Then move
      the five overlays onto it.
      Why it is worth doing: a user should not have to learn a different way out of each
      overlay, and a visible X should not be something each overlay remembers to add — the
      Faceplate only got one because the user asked. It also deletes five copies of the same
      backdrop and key-handling code.
      Backlog — not part of the Faceplate work.

- [ ] **Dissolve `Orchestrator.HandleGesture`'s switch into the gesture binding table.**
      `Orchestrator.cs` is ~703 lines doing three jobs: a 34-arm switch turning gestures into
      app actions (~195 lines), the scratch engine (~276 lines), and the 60 Hz frame pump.
      The mechanism to fix this already exists — `GestureBindings` takes a
      `Dictionary<string, Action<Gesture>>`. Today `App.axaml.cs:287` builds it as a
      placeholder: all 34 ids point at the single `HandleGesture` method.
      Replace that with one named lambda per gesture, assembled from five small classes
      grouped by what they actually touch: `TransportBindings` (play, both CUEs, sync),
      `MixerBindings` (EQ, stem levels, filter, volume, tempo, crossfader), `PadBindings`
      (stem pads, echo, pad modes), `LoopBindings` (beat loop, grid nudge) and
      `BrowseBindings` (turn, tap, hold, load). The switch then disappears.
      **Three things this unlocks, which are the real reason to do it:**
      1. Coverage becomes a test — `Assert.Empty(GestureIds.All.Except(table.BoundIds))`.
         Today that is unknowable; a reviewer already found the switch's `default:` arm is
         unreachable and nobody can currently assert it.
      2. Each group becomes testable alone with a fake deck. Those 34 cases have no direct
         tests today.
      3. The Orchestrator stops being a god object, leaving the scratch engine as an obvious
         second extraction — 276 self-contained lines that are the riskiest and least tested
         code in the app, and the only place where a regression is felt rather than caught.
      Do it on its own branch, after the Faceplate work is committed. It touches the audio
      path that two Faceplate tasks went out of their way not to disturb, so it must not
      share a diff with a UI feature.

- [ ] **Beat-based roll on PAD FX1 pad 2.** Sholto already has a beat-based echo on PAD FX1
      pad 1 (`GestureIds.PadEcho` = `pad.padfx1.echo`, mapped at
      `DdjFlx4Mapping.cs:107` from `PadFx1NoteBase`). Add a roll in the second pad slot
      (`PadFx1NoteBase + 1`), sized in beats the same way the echo is.
      A roll repeats a short slice of the track in time with the beatgrid while the pad is
      held, then returns the playhead to where it would have been on release. So it needs
      the beat length from the analysis, a capture buffer, and a "catch up on release"
      behaviour that the echo does not need.
      Work to do: a new `ControllerEvent` and gesture id, a mapping arm next to the echo
      one, a binding in the Orchestrator, a pad LED while held, and a guide entry in
      `src/Sholto.Faceplate/Devices/DdjFlx4/ddj-flx4.guide.json`.
      Backlog.

- [ ] **Eyes, hands and ears — make Sholto drivable by an agent.** Today an assistant can
      build the app, launch it detached, screenshot the window by geometry
      (`wmctrl -lG` + `ffmpeg -f x11grab`) and read its log — but it cannot click, type,
      turn a jog wheel, or hear a single sound. Every bug that lives behind an interaction
      or in the audio itself (deck-1 stems dead when the same track is on both decks; the
      backspin jolting left then jumping far left) is therefore stuck: only a human can
      observe it, so only a human can confirm a fix.
      Three capabilities, in the order they pay off:

      1. **Eyes — a state dump.** A `state` command that serialises what the app believes:
         deck positions, playhead, channel/master gain, crossfader, tempo, beatgrid
         confidence, which analysis steps completed or failed. JSON on demand. This alone
         replaces most screenshot-reading, because it gives numbers instead of pixels.
      2. **Hands — a control channel.** A Unix domain socket or loopback HTTP endpoint,
         dev builds only, behind an env var (e.g. `SHOLTO_CONTROL_SOCKET`), accepting the
         same gestures the controller sends: load deck N, play, cue, nudge, set fader,
         hit a pad. Pair it with `Avalonia.Headless` tests (`window.MouseDown`,
         `window.KeyPress`, `window.CaptureRenderedFrame`) so the real UI tree can be
         driven in CI with no display — `Sholto.App.Tests` exercises ViewModels today and
         never touches the UI, which is why the Grid z-order and clipping traps in
         CLAUDE.md keep having to be found by eye.
      3. **Ears — audio capture and measurement.** The output devices expose PipeWire
         `.monitor` sources, so `parec -d <sink>.monitor` records exactly what came out of
         the speakers. Measuring RMS per channel, spectral content and silence turns
         "did the crossfader work", "did the stem mute actually mute" and "what shape is
         the backspin" into assertions with numbers. Without this, audio verification can
         never be anything but the user listening.

      Worth building before the next audio-path change, not after: it is the difference
      between a fix that is confirmed and a fix that is hoped for.

- [ ] **Make the external-tool layer `IOptions`-aware, ready for Windows.** The new
      `src/Sholto.Analysis/ExternalTools/` layer got the *shape* right — one resolver, one
      runner, a descriptor per tool — but it still hardcodes POSIX facts. Scope this task to
      the third-party tooling only; the PipeWire/pactl routing in `Sholto.Audio` and the
      `libpulse.so` work in the installers are a separate, larger job.

      What is currently fixed in code and needs to come from options:
      - `ExternalToolBinary.cs:25-26` — the search path is literally `~/.local/bin`,
        `/usr/local/bin`, `/usr/bin`, then `PATH`. On Windows none of those exist; the
        equivalents are `%LOCALAPPDATA%\uv\tools\...`, `%ProgramFiles%` and `PATH`.
      - Binary names carry no extension: `DemucsTool.cs:14` `"demucs"`,
        `MadmomTool.cs:10` `"DBNDownBeatTracker"`,
        `FfmpegDecodeStrategy.cs:30` `"ffmpeg"`. Windows needs `.exe`.
      - `DemucsTool` hardcodes the `htdemucs` output-directory name. That is a *model*
        name, not a platform fact, and it changes if the default model ever changes —
        worth lifting for the same reason.

      Shape it like the existing options types (`FeatureOptions`, `ScratchOptions`,
      `MagnetismOptions`): an `ExternalToolOptions` POCO with defaults in code, supplied
      through the standard `IOptions<T>` pipeline and wired in `App.axaml.cs` next to the
      others. Defaults must be chosen per-platform at construction
      (`OperatingSystem.IsWindows()`), not by editing the file. Note the precedent at
      `App.axaml.cs:272` — `Sholto.Controller` deliberately does not take a
      `Microsoft.Extensions.Options` dependency and gets a plain POCO handed to it;
      `Sholto.Analysis` should follow that, so the analysis project stays free of the
      options package.

      **The constraint that makes this more than a refactor:** `sholto-deps.sh` mirrors the
      resolver's search order and the `htdemucs/<stem>.wav` layout in bash, and nothing
      keeps the two in step. Lifting these into options makes that drift *easier*, not
      harder — so this task should land together with, or after, the `--self-check` idea in
      the external-tools plan (end of `~/Projects/sholto.md`), where the app itself becomes
      the single definition of "working" and the shell script just calls it.

      Benefit beyond Windows: a user whose tools live somewhere unusual (conda, pipx, a
      Nix profile) could point Sholto at them without a rebuild.

- [ ] **Audit the static classes and convert the ones holding state to constructor injection.**
      There are 35 `public static class` types across `src/`. Most are fine and must be left
      alone — the point of this task is to apply a test, not to convert everything.

      **The test:** does the type touch the outside world, or hold state that outlives a
      call? If it reads the environment, the filesystem, a process, a device, a clock or a
      database — inject it. If it is pure functions over its arguments, or a bag of
      constants, leave it static.

      **Leave static (pure / constants):** `CamelotKeys`, `Beatgrid`, `KeyAnalyzer`,
      `SongSegmentAnalyzer`, `VocalRegionAnalyzer`, `SettingsKeys`, `GestureIds`,
      `ExternalToolNames`, `TagNameNormalizer`, `AnalysisCodec`, `KeyAnalysisCodec`,
      `LibrarySearch`, `GestureToControl`, `MappingRegistry`.

      **Convert (touches the world):** `ExternalToolBinary`, `ExternalToolRunner`,
      `MadmomBeatAnalyzer`, `DemucsStemAnalyzer` — covered by the
      composition-root work, do those first and let them set the pattern. Then:
      `AudioFileDecoder` (owns the decode-strategy registry), `AudioDevices`,
      `PipeWireRouter` (shells out to `pactl`/`pw-link`), `DatabaseBridge`, `SholtoStorage`,
      `ThemeContext`, `ProcessStats`, `FaceplateDocLoader`, `TrackScanner`.

      **Why it is worth doing, concretely — not style:**
      - `MadmomBeatAnalyzer.BinaryPath` is a static property initialised at *type
        load*. Install a tool while Sholto is running and it is never seen until
        restart.
      - Static state cannot be varied per platform, which is what the Windows options work
        needs.
      - It cannot be substituted in a test or in the planned agent harness. The only current
        seam is `ExternalToolBinary`'s `internal` overload plus `InternalsVisibleTo` — a
        workaround for exactly this.
      - Static analysers are why `Deck` reaches directly into `Sholto.Analysis` at
        `Deck.cs:544,549,562` instead of being handed what it needs.

      **Do NOT add a DI container.** The codebase composes by hand in `App.axaml.cs` and that
      is the house style; a container is a separate decision to be argued on its own merits.

      Convert incrementally, one type per change, each verified by the suite staying green —
      not as one sweeping refactor.

- [ ] **Finish decomposing `Deck` (stages 3–5).** Parked 2026-09-12 after two stages, at a
      deliberate stopping point: the next cluster is scratch, the riskiest code in the app,
      and it should not start on top of two unverified-by-ear stages.

      **Done so far** — `Deck.cs` 1453 → 994 lines, public surface unchanged, all suites green:
      - stage 1: `LoopControl`/`ILoopControl`, `BeatgridControl`/`IBeatgridControl`
      - stage 2: `MixerOutputControl`/`IMixerOutputControl`, `PitchTempoControl`/`IPitchTempoControl`

      **The pattern** (follow it, do not invent a second one): component holds the logic,
      `Deck` keeps its public surface and delegates one line per member, relaying component
      events to its own. No component gets a back-reference to `Deck` — pass narrow accessor
      delegates (`Func<TrackAnalysis>`, `Func<IVarispeedProvider?>`) for state `Deck` replaces
      on load.

      **Remaining clusters:**
      - **transport** — `Play`, `Pause`, `TogglePlay`, `SeekRelative`, `SeekToFraction`
      - **loading** — `BeginLoad`, `LoadStreaming`, `Load`, `Unload`, `AttachEngine`,
        `SwitchToStemMode`, `TearDownPlayers` (also owns the SoundFlow graph wiring)
      - **scratch** — `CanScratch`, `ScratchRate`, `EndScratch`, `VarispeedProvider`.
        RISKIEST: most timing-sensitive code in the app, and the backspin complaint has never
        been reproduced or ruled out. Do this one alone, with a human listening.
      - **stems** — `SetStemGroup`, `SetStemGroupLevel`
      - **analysis orchestration** — `AnalysisProvider`, `Analysis`, `Reporter`,
        `KeyCacheGet/Put`. NOT a mechanical move: `Deck` orchestrating analysis is a layering
        fault, and there is no owner to hoist it to — `TrackAnalysis` is a passive typed-event
        container and `AnalysisProvider` is a cache-aside lookup for `BasicAnalysis` only.
        Inventing that owner is a design task, on its own branch.

      **Before resuming, two things that would make it materially safer:**
      1. **Commit first.** Stage 2's before/after audio proof had to hand-reconstruct the
         pre-edit file because ~100 files were uncommitted and `HEAD` was three stages stale.
         With a checkpoint commit, every stage's proof becomes `git show HEAD:...` and is
         rigorous instead of probable.
      2. **Listen to it.** Neither stage's harness exercised the real four-stem demucs mix
         (stage 1 pointed all stems at the source file; stage 2 made stems unavailable), and
         both pulled `Process` synchronously rather than from the real-time audio thread. So
         nothing has tested ordering, contention, or the background stem-swap race. Worth ten
         minutes: volume/crossfader for zipper noise, EQ and filter sweeps for clicks, echo
         tail across a tempo change, cue bus isolation, a loop seam at speed, a backspin.

- [ ] **Make `Orchestrator` the glue between three entities: control surface, keyboard, app.**
      Deferred 2026-09-12 — the design is agreed and sound, but it should start from a
      committed, listened-to baseline rather than on top of ~100 uncommitted files. See the
      note at the end of this entry.

      **The fault it fixes.** `App.axaml.cs` is the glue today, not `Orchestrator`: lines
      ~375–477 construct the controller, subscribe `.Action`, dispatch into the gesture bus,
      and relay the Orchestrator's own LED events back to it. Meanwhile the keyboard never
      touches any of that — `MainWindow.OnGlobalKeyDown` calls the ViewModel directly. So a
      keyboard Play and an FLX4 Play reach the same outcome by two entirely separate paths.
      That is two implementations of one intent, free to drift, and it is why Bench driving
      the UI proved nothing about jog behaviour.

      **Target shape:**
      ```csharp
      Orchestrator(IControlSurface surface,   // FLX4 or Bench — exists already
                   IKeyboard      keyboard,   // new port
                   IApplication   app,        // composition of the four existing roles
                   … its own deps: dbFactory, options, recognizer, decoder)

      public interface IApplication : IDecks, ITransport, IMixer, IBrowser { }
      ```
      `IApplication` is interface COMPOSITION and must never gain members of its own —
      the moment it does, it is `IDeckHost` again.

      Signals: `IControlSurface` ⇄ Orchestrator (events up, LEDs back), `IKeyboard` →
      Orchestrator → `IApplication`. The LED relay moves out of `App.axaml.cs` into the
      Orchestrator, which already raises those events.

      **Then the factory.** Verified against the Avalonia docs: `AppBuilder` has a
      `Configure<TApp>(Func<TApp> appFactory)` overload, so `App` CAN take constructor
      dependencies — `Program.BuildAvaloniaApp()` supplies them. An abstract factory fits,
      because the three entities must be consistent with each other:
      ```csharp
      public interface ISholtoEntities
      {
          IControlSurface CreateControlSurface();
          IKeyboard       CreateKeyboard();
          IApplication    CreateApplication();
      }
      ```
      `LiveEntities` → real FLX4, real keyboard, real `MainViewModel`.
      `BenchEntities` → `ScriptedControlSurface`, scripted keyboard, same real ViewModel.

      **Scope boundary — do not breach it.** The factory assembles the THREE ENTITIES only.
      `InitializeServices` still builds the ~20 leaf collaborators (tool options, finder,
      `ToolSet`, the three analysers, decoder, storage, theme, loop debug, scanner, MIDI
      manager, mappings). Migrating those into the factory recreates the god object this
      whole review dismantled. Three layers: composition root builds leaves → factory
      assembles entities → Orchestrator binds them.

      **The payoff.** `BenchAppComposer` currently RE-IMPLEMENTS App's wiring by hand, so it
      can drift and then Bench tests a configuration that does not ship. With the factory it
      substitutes into the real path instead of duplicating it.

      **The judgement call in it.** Not every keypress is an app gesture. `OnGlobalKeyDown`
      also handles Escape-closes-the-tag-editor, Enter-commits, Tab, Up/Down through
      suggestions, Escape-closes-the-faceplate. Those are view-local and MUST stay in the
      view; `IKeyboard` carries only genuine app shortcuts. That split is the part needing
      care — everything else is mechanical.

      **Do first:** commit the current tree, and play a track for ten minutes (see the
      listening checklist in the `Deck` decomposition entry above). This change touches
      `App.axaml.cs` (666 lines) and `MainWindow.axaml.cs`, the two highest-churn files
      left; starting it from an unverified baseline means bisecting two large structural
      changes with no commits to bisect against.

- [ ] **Audit class cohesion and propose a folder structure (run LAST, after the refactor).**
      Deliberately sequenced at the end: folder structure should follow the boundaries the
      refactor establishes, not guess at them beforehand. Running it early would enshrine
      groupings that the SRP and port work is still moving.

      **Why it is needed.** Most projects are flat — `src/Sholto.Audio/` has ~25 files at the
      root mixing decode strategies, deck components, data providers, routing and ports;
      `src/Sholto.App/` has ~35 mixing view models, theming, options, the composition root
      and the orchestrator. Only `Sholto.Controller` (`Model/`, `Mappings/`, `Gestures/`),
      `Sholto.Faceplate` (`Model/`, `Views/`, `ViewModels/`) and `tools/Sholto.Bench`
      (`Sound/`, `Appearance/`, `Behaviour/`, `Scenario/`, `Ui/`, `Rendering/`, `State/`)
      have real structure — and Bench's is the one that reads best, because its folders name
      what the code is FOR rather than what it IS.

      **What the audit should do:**
      - Group by cohesion — what changes together, what shares a reason to change — not by
        technical kind ("Interfaces/", "Helpers/", "Models/" are anti-patterns here).
      - Name folders for purpose, the way Bench's `Sound`/`Appearance`/`Behaviour` do.
      - Report which types are ALREADY well-grouped and should not move. Churn has a cost and
        this tree has just absorbed a lot of it.
      - Flag any grouping that would need a type to move BETWEEN projects — that is a pass-3
        onion question (an interface belongs with its consumer; if moving it creates a cycle,
        something on the other end is doing two jobs), not a folder question, and must be
        raised rather than silently done.
      - Explicitly call out where a proposed folder would cut across an existing port/adapter
        boundary, since those boundaries are load-bearing for the BUSL licensing position.

      **Deliverable is a PROPOSAL, not a mass move.** Namespaces follow folders by convention
      in this codebase, so relocating a file is a namespace change and a churn of `using`
      lines across the tree. The audit writes its recommendation to `~/Projects/sholto.md`;
      the user decides what actually moves, and in what order.

- [ ] **Real-time audio performance: measure first, then change GC settings.** Carved out
      2026-09-12 from a runtime/packaging analysis. Full reasoning in `~/Projects/sholto.md`
      under "Analysis — .NET runtime & packaging for real-time audio". Do the measurement
      step FIRST — every item below is currently [reasoned], not [measured], and this
      project has not measured its audio path once.

      **The deadline, from the real code** (`AudioEngine.cs:101-117`): 48 kHz, 20 ms period
      x 3 buffers = 960 frames per callback, ~50 callbacks/sec, ~20 ms average budget and
      roughly 40 ms of one-shot slack before a dropout. Comfortable — this is "survive a GC"
      territory, not hard real-time. The metric is NEVER MISSING A DEADLINE, not throughput:
      a 30 ms pause with a 20 ms buffer is an audible dropout even if averages improve.

      **STEP 0 — build the callback histogram.** A `Stopwatch` plus a pre-allocated bucket
      array in `CueOutputRouter.GenerateAudio` (`CueOutputRouter.cs:45`), dumped on demand.
      No allocation, negligible cost. This turns every item below into a 20-minute A/B
      instead of an argument. Nothing else here should be changed before it exists.

      **1. `<ServerGarbageCollection>` is ON and its justification is factually wrong.**
      `src/Sholto.App/Sholto.App.csproj:13`, with a comment (lines 10-12) claiming
      "workstation GC pauses every managed thread... server + concurrent keeps pauses short".
      **Both** modes suspend every managed thread for gen0/gen1; concurrent GC only affects
      gen2. What Server GC actually changes is the trade — a larger gen0 budget means RARER
      BUT LONGER pauses, plus up to 12 GC threads on this 12-core box contending with the
      audio and render threads. Backwards for a deadline app. Confirmed live:
      `System.GC.Server: true` in the emitted `Sholto.App.runtimeconfig.json`.
      → Set `false`, keep `<ConcurrentGarbageCollection>true`. Fix the comment too.

      **2. `PublishReadyToRun=true`.** The risk is not tier-0 being slow in general — it is
      COLD JIT ON THE AUDIO THREAD the first time a loop, effect or stem path is reached
      mid-set. R2R removes that. Caveat: never combine with
      `EnableCompressionInSingleFile`, which defeats R2R's mapped-code path. Costs binary
      size and build time.

      **3. `GCSettings.LatencyMode = SustainedLowLatency` while playing.** Small, real,
      gen2-only.

      **The bigger wins are in the CODE, not the config:**
      - `SoundFlow.dll` 1.4.1 rents from `ArrayPool<float>.Shared` on EVERY
        `SoundComponent.Process` — one per deck per buffer via `CueOutputRouter.cs:62`.
        Warm pool is allocation-free, but a starved bucket allocates ON THE AUDIO THREAD.
        Largest exposure, and it is in third-party code.
      - Strongest existing evidence for all of this: SoundFlow's own `PlaybackSpeed` was
        already rejected because WSOLA time-stretching allocated per block and froze the UI.
        Same failure mode. Allocation, not GC mode, is where the pain comes from.

      **Explicitly WRONG for this workload — do not let anyone "optimise" these in:**
      `TryStartNoGCRegion` (re-arming triggers the very pause it avoids), `TieredCompilation=false`
      (makes cold JIT worse, strictly worse than today), disabling `TieredPGO` (guarded
      devirtualisation is earning its keep on the per-buffer `IMixSource`/`SoundComponent.Process`
      dispatch), `GCConserveMemory`, `GCLargePages`, and shrinking the buffer period before
      measuring. **`PublishTrimmed` is a hard no** — `AvaloniaUseCompiledBindingsByDefault=false`
      (csproj:8) means reflection bindings, plus EF Core 10 and avares theme loading.
      **Native AOT: not viable and would not help** — reflection-bound Avalonia and EF Core
      make it a multi-week port, it uses the same GC, and it only fixes the JIT that R2R
      already fixes.

      **Also code, not config:** `CueOutputRouter._scratch` (line 52) and
      `StemMixDataProvider.BuildLoopTail` (150-153) were checked and are FINE — recorded so
      nobody "fixes" them.

      **Machine context (measured 2026-09-12):** PipeWire `clock.quantum = 1024` @ 48 kHz =
      21.33 ms, reached through the PulseAudio compat server — so our callback is a
      socket-fed client, not a hard-RT device thread. PipeWire's own threads already run at
      `rt.prio 88`. `ulimit -r` is 0 and the user is not in `@pipewire`, but `rtkit-daemon`
      is running, so RT priority is reachable only via rtkit's D-Bus route. It would not
      help against the GC anyway: EE suspension stops a SCHED_FIFO thread too.
