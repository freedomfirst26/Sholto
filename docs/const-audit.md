# Const audit: what stays `const`, what moves to options

Developer note. It applies the "Tunable values go in options" rule in `CLAUDE.md` to every
non-string `const` under `src/`, and records the verdict for each so later work can pick up
the moves one options class at a time.

## How the list was taken

```bash
grep -rnE '\bconst\b' src --include=*.cs | grep -vE 'const string'
```

Run on `origin/main` before this change, it gives **206 lines**: 203 declaration lines and
3 doc-comment lines that only mention the word (listed at the end so the count reconciles).
Five declaration lines declare more than one name (`BloomMin, BloomMax`,
`Normal, FadingOut, FadingIn`, `Drums, Vocals, Bass, Other`); one verdict covers the line.
Line numbers are those on `origin/main` before this change.

## Verdicts

- **keep**: maths, sentinel or guard, format or protocol fact (incl. hardware layout of the
  DDJ-FLX4, serialization versions, shared cross-project contracts), layout or drawing pixels,
  frame order, version.
- **move**: something a DJ or developer might plausibly tune (timing, threshold, capacity), or a
  tunable duplicated in two places. Each move names the options class it should join.
- **moved (this job)**: done here; now read from `MagnetismOptions`.

Audio-thread classes (`EchoEffect`, `DjFilter`, `BiquadEq3Band`, `StemFadeIn`,
`ScratchDataProvider`, `BeatRepeatEffect`) may only take a move if the value is read once in
the constructor into a field, never per block or per sample.

## Sholto.App

### src/Sholto.App/Performance/MagnetSnap.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 11 | `EngageThreshold = 0.3` | moved (this job) | Snap/glow alignment threshold a DJ may want looser or tighter; now `MagnetismOptions.EngageThreshold`. |
| 12 | `DisengageThreshold = 0.15` | moved (this job) | Re-arm hysteresis threshold; now `MagnetismOptions.DisengageThreshold`. |
| 77 | `window = 0.15` | moved (this job) | Bar-start misalignment window in seconds, a feel tunable; now `MagnetismOptions.BarWindowSeconds`. |

### src/Sholto.App/Loading/LoadGuard.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 13 | `FrameOrder = 50` | keep | Frame order: fixes when this runs in the frame-clock sequence, not a tunable. |

### src/Sholto.App/Loading/LoadUndo.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 12 | `FrameOrder = 50` | keep | Frame order, as LoadGuard. |

### src/Sholto.App/Decks/DeckHotCues.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 13 | `PadCount = 8` | keep | Hardware fact: the FLX4 has eight performance pads per deck. |

### src/Sholto.App/Decks/DeckPlayPhase.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 10 | `FlashHalfPeriodMs = 400` | move → `DeckPlayOptions` (new, Sholto.App) | Timing of the end-of-track flash; a DJ may want a faster or slower warning. |
| 12 | `NearEndPosition = 0.9` | move → `DeckPlayOptions` (new, Sholto.App) | Threshold for "near the end" warning; plausibly tuned per DJ. |

### src/Sholto.App/CueRouting.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 11 | `DeckCount = 2` | keep | Format fact: Sholto is a two-deck app. |

### src/Sholto.App/Glance/GlanceRanker.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 8 | `MissingDelta = 99.0` | keep | Sentinel: "no value" delta that sorts a missing BPM/key last. |

## Sholto.Data

### src/Sholto.Data/StemMuteChanged.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 8 | `StemsPerDeck = 3` | keep | Shared cross-project contract (controller feedback reads it). |

### src/Sholto.Data/Origin.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `NoDeck = -1` | keep | Sentinel. |

### src/Sholto.Data/DataBus.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 13 | `MaxPublishDepth = 16` | keep | Guard: loop detection on nested publishes, not a feel value. |

### src/Sholto.Data/SetBackspinTime.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 9 | `Min = 0.0` | keep | Shared contract: the command's valid range, read by UI and App alike. |
| 12 | `Max = 3.0` | keep | Shared contract, as Min. |
| 15 | `Default = 0.6` | keep | Shared contract: the setting's default lives with the command that carries it. |

### src/Sholto.Data/SetBackspinDistance.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 9 | `Min = 0.0` | keep | Shared contract: valid range of the command. |
| 12 | `Max = 16.0` | keep | Shared contract, as Min. |
| 15 | `Default = 2.0` | keep | Shared contract: the setting's default. |

## Sholto.App.Library

### src/Sholto.App.Library/Tags/ITagService.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 6 | `MaxTagsPerTrack = 128` | keep | Guard: data-integrity cap, not something a DJ tunes. |

### src/Sholto.App.Library/Tags/TagNameNormalizer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 11 | `MaxLength = 100` | keep | Guard: tag-name length cap. |

## Sholto.App.Dsp

### src/Sholto.App.Dsp/WaveformBandFrequencies.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `LowMidHz = 250f` | keep | Shared contract: one crossover the EQ and waveform must agree on; already the single definition. |
| 17 | `MidHighHz = 4000f` | keep | Shared contract, as LowMidHz. |

## Sholto.App.Analysis

### src/Sholto.App.Analysis/EngineAudio.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 7 | `SampleRate = 48000` | keep | Format fact and shared contract: the engine's sample rate. |

### src/Sholto.App.Analysis/Analyzers/Beats/BeatgridAnalyzer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 89 | `AnchorWindowBars = 16` | keep | Algorithm parameter fixed by the analysis design; changing it invalidates cached grids, not a user tunable. |

### src/Sholto.App.Analysis/Analyzers/Waveform/WaveformDefaults.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `SamplesPerPeak = 1024` | keep | Format fact: peak resolution baked into stored analysis. |

### src/Sholto.App.Analysis/Analyzers/Vocals/VocalRegionAnalyzer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 21 | `PresenceThreshold = 0.12f` | move → `VocalRegionOptions` (new, Sholto.App.Analysis) | Detection threshold a developer tunes against real tracks. |
| 22 | `MinSpanSec = 0.20` | move → `VocalRegionOptions` | Timing: shortest vocal span kept. |
| 23 | `MergeGapSec = 0.25` | move → `VocalRegionOptions` | Timing: gap bridged between vocal spans. |

### src/Sholto.App.Analysis/Analyzers/Keys/KeyAnalyzer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 97 | `frameSize = 4096` | keep | Maths/format: FFT frame size the chroma bins are built for. |
| 98 | `firstMidi = 36` | keep | Maths: lowest pitch (C2) of the chroma range. |
| 99 | `lastMidi = 95` | keep | Maths: highest pitch (B6). |
| 117 | `framesPerChunk = 32` | keep | Buffer layout: processing chunk size, no audible or visible effect. |

## Sholto.App.Audio

### src/Sholto.App.Audio/EchoEffect.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 61 | `Feedback = 0.55f` | move → `EffectOptions` (new, Sholto.App.Audio), read once in ctor | Echo character a DJ might tune. |
| 62 | `Wet = 0.5f` | move → `EffectOptions`, read once in ctor | Echo level a DJ might tune. |
| 63 | `Dry = 1.0f` | keep | Maths: unity dry path, not a mix choice. |
| 67 | `CapacitySeconds = 4.0` | move → `EffectOptions` | Capacity, duplicated in `BeatRepeatEffect.cs:50`. |

### src/Sholto.App.Audio/BeatRepeatEffect.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 50 | `CapacitySeconds = 4.0` | move → `EffectOptions` | Capacity, duplicated in `EchoEffect.cs:67`. |

### src/Sholto.App.Audio/StemFadeIn.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 18 | `FadeFrames = 256` | move → `DeclickOptions` (new, Sholto.App.Audio), read once in ctor | De-click fade length, duplicated in `ScratchDataProvider.cs:48`. |

### src/Sholto.App.Audio/ScratchDataProvider.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 36 | `NoPendingSeek = long.MinValue` | keep | Sentinel. |
| 48 | `FadeFrames = 256` | move → `DeclickOptions`, read once in ctor | De-click fade length, duplicated in `StemFadeIn.cs:18`. |

### src/Sholto.App.Audio/StemMixDataProvider.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 31 | `StemCount = 4` | keep | Format fact: Demucs four-stem layout. |
| 32 | `Drums = 0, Vocals = 1, Bass = 2, Other = 3` | keep | Format fact: stem buffer indices. |
| 48 | `NoPendingSeek = long.MinValue` | keep | Sentinel. |

### src/Sholto.App.Audio/DjFilter.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 52 | `LpMinHz = 80f` | move → `FilterOptions` (new, Sholto.App.Audio), read once in ctor | Cutoff range a DJ might tune (how far the low-pass closes). |
| 53 | `LpMaxHz = 18000f` | move → `FilterOptions` | Cutoff range, as LpMinHz. |
| 54 | `HpMinHz = 20f` | move → `FilterOptions` | Cutoff range of the high-pass. |
| 55 | `HpMaxHz = 10000f` | move → `FilterOptions` | Cutoff range of the high-pass. |
| 56 | `DeadZone = 0.02f` | keep | Guard: bypass band around centre so a knob at rest is dry. |
| 61 | `Q = 2.0f` | keep | Maths: filter coefficient (resonance) the drive was voiced against. |
| 69 | `Drive = 2.0f` | keep | Maths: filter coefficient paired with Q. |

### src/Sholto.App.Audio/BiquadEq3Band.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 49 | `LowMidHz = WaveformBandFrequencies.LowMidHz` | keep | Alias of the shared crossover contract. |
| 50 | `MidHighHz = WaveformBandFrequencies.MidHighHz` | keep | Alias of the shared crossover contract. |
| 87 | `bwQ = 0.7071068f` | keep | Maths: 1/√2 Butterworth Q. |
| 113 | `GainSmoothAlpha = 0.00045f` | keep | Maths: filter coefficient for click-free gain changes. |

### src/Sholto.App.Audio/AudioFileDecoder.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 27 | `TargetSampleRate = EngineAudio.SampleRate` | keep | Format fact: alias of the engine sample rate. |
| 28 | `TargetChannels = 2` | keep | Format fact: stereo decode. |

### src/Sholto.App.Audio/PipeWireRouter.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 167 | `attempts = 10` | move → `AudioRoutingOptions` (new, Sholto.App.Audio) | Retry count a developer tunes for slow PipeWire startups. |
| 168 | `delayMs = 300` | move → `AudioRoutingOptions` | Retry timing, as attempts. |

### src/Sholto.App.Audio/EffectOutputStage.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 26 | `ResetRampSeconds = 0.005` | keep | Guard: 5 ms anti-click ramp, not audible as a choice. |
| 28 | `ScratchFrames = 256` | keep | Buffer layout: scratch buffer size. |
| 30 | `Normal = 0, FadingOut = 1, FadingIn = 2` | keep | Sentinel state values. |

### src/Sholto.App.Audio/DeckEffect.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 38 | `MixParamId = 100` | keep | Protocol fact: parameter id. |

### src/Sholto.App.Audio/DeckMixer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 39 | `DefaultBpm = 128.0` | keep | Sentinel fallback before analysis, already the single definition shared by echo and roll. |

### src/Sholto.App.Audio/DeckLooping.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 31 | `MinLoopLengthSamples = 64` | keep | Guard: sanity floor so the wrap crossfade fits. |

## Sholto.App.Storage

### src/Sholto.App.Storage/KeyAnalysisSerializer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 20 | `Version = 1` | keep | Version: serialization format. |

### src/Sholto.App.Storage/BasicAnalysisSerializer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 30 | `Version = 4` | keep | Version: serialization format. |

### src/Sholto.App.Storage/SqlitePragmaInterceptor.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 27 | `BusyTimeoutMs = 5000` | move → `DatabaseOptions` (new, Sholto.App.Storage) | Timing a developer tunes for lock contention. |

## Sholto.App.ExternalTools

### src/Sholto.App.ExternalTools/ProcessOutputTail.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 19 | `MaxLineLength = 400` | keep | Guard: truncates runaway subprocess log lines. |

## Sholto.Interface.Controller

### src/Sholto.Interface.Controller/ControllerFeedback.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 20 | `Decks = 2` | keep | Hardware fact: two decks on the FLX4. |
| 21 | `PadsPerPage = 8` | keep | Hardware fact (same fact in `Controller.cs:35`, `DdjFlx4Mapping.cs:21`; a fact, not a tunable). |
| 22 | `Stems = StemMuteChanged.StemsPerDeck` | keep | Alias of the shared contract. |
| 24 | `EchoPad = 0` | keep | Protocol fact: pad index for the LED map. |
| 25 | `RollPad = 1` | keep | Protocol fact: pad index. |
| 26 | `SoloPadBase = 4` | keep | Protocol fact: first solo pad index. |

### src/Sholto.Interface.Controller/ControllerInput.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 21 | `FrameOrder = 100` | keep | Frame order. |

### src/Sholto.Interface.Controller/Controller.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 35 | `PadsPerPage = 8` | keep | Hardware fact. |
| 36 | `PageCount = 4` | keep | Hardware fact: four pad modes. |
| 53 | `PageUnknown = -1` | keep | Sentinel. |

## Sholto.Interface.Controller.Mappings

### src/Sholto.Interface.Controller.Mappings/DdjFlx4/DdjFlx4Mapping.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 21 | `PadsPerPage = 8` | keep | Hardware fact. |
| 22 | `PadPageCount = 4` | keep | Hardware fact. |

## Sholto.Interface.Faceplate

### src/Sholto.Interface.Faceplate/Views/FaceplateBoard.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 169 | `DimmedRestBloom = 0.10` | keep | Layout/drawing: opacity of a drawn bloom. |

### src/Sholto.Interface.Faceplate/Views/FaceplatePanelPartsFactory.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `ChipLineHeight = 24` | keep | Layout pixels. |

### src/Sholto.Interface.Faceplate/Views/FaceplateRowVisualFactory.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 19 | `RowGutter = 10` | keep | Layout pixels. |

### src/Sholto.Interface.Faceplate/Views/FaceplateShapeFactory.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 17 | `BloomFraction = 0.16` | keep | Layout/drawing proportion. |
| 18 | `BloomMin = 4, BloomMax = 10` | keep | Layout pixels. |
| 22 | `HaloMultiple = 1.6` | keep | Layout/drawing proportion. |

## Sholto.Interface.MainUI

### src/Sholto.Interface.MainUI/ViewModels/DiscBloom.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 11 | `AttackSeconds = 0.030` | keep | Drawing envelope of a visual effect, voiced with the scales below. |
| 12 | `ReleaseSeconds = 0.250` | keep | Drawing envelope, as attack. |
| 13 | `LowScale = 1.15` | keep | Drawing weight per band. |
| 14 | `MidScale = 0.8` | keep | Drawing weight per band. |
| 15 | `HighScale = 0.9` | keep | Drawing weight per band. |
| 17 | `MaxStepSeconds = 0.1` | move → `DeckViewOptions` (`MaxFrameStepSeconds`) | Frame-step clamp duplicated in `WaveformPreviewScroll.cs:13` and `DeckSlotViewModel.cs:22`. |

### src/Sholto.Interface.MainUI/ViewModels/WaveformPreviewScroll.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 11 | `ClockOrder = 100` | keep | Frame order. |
| 13 | `MaxStepSeconds = 0.1` | move → `DeckViewOptions` (`MaxFrameStepSeconds`) | Duplicate of `DiscBloom.cs:17`. |

### src/Sholto.Interface.MainUI/ViewModels/Glance/DeckSlotViewModel.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 19 | `Steps = 360` | keep | Maths: degrees per turn. |
| 21 | `BeatsPerBar = 4` | keep | Maths: glossary fact (bar = 4 beats). |
| 22 | `LongestFrameSeconds = 0.1` | move → `DeckViewOptions` (`MaxFrameStepSeconds`) | Duplicate of `DiscBloom.cs:17`. |

### src/Sholto.Interface.MainUI/ViewModels/Glance/GlanceHeaderViewModel.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `ClockOrder = 110` | keep | Frame order. |

### src/Sholto.Interface.MainUI/ViewModels/Glance/GlanceViewModel.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 26 | `ClockOrder = 100` | keep | Frame order. |

### src/Sholto.Interface.MainUI/ViewModels/LayoutWizardViewModel.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 30 | `Columns = 4` | keep | Layout: theme grid columns. |
| 33 | `StartFraction = 0.2` | keep | Layout: where the demo preview starts; fixed by the demo content. |

### src/Sholto.Interface.MainUI/ViewModels/DeckViewModel.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 416 | `LoadingInfoOpacity = 0.3` | keep | Layout/drawing opacity. |

### src/Sholto.Interface.MainUI/ViewModels/TagEditorViewModel.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 168 | `maxSuggestions = 5` | move → `GlanceViewOptions` (`TagSuggestionLimit`) | Capacity: how many suggestions show, like the existing `TagRailLimit`. |

### src/Sholto.Interface.MainUI/Views/SearchOverlayResultsSync.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `FlashMs = 600` | move → `GlanceViewOptions` (`JoinFlashMs`) | UI timing, alongside the existing indicator delays. |

### src/Sholto.Interface.MainUI/Views/SearchOverlayKeyHandler.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 11 | `PageSize = 8` | keep | Layout: rows per page-up/down, matches visible rows. |

### src/Sholto.Interface.MainUI/Theming/AppIconFactory.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 16 | `size = 256` | keep | Format: icon bitmap size. |
| 34 | `textSize = 220` | keep | Layout pixels. |

### src/Sholto.Interface.MainUI/Controls/Minimap/MinimapRenderer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 16 | `TintAlpha = 33` | keep | Drawing alpha applied via `WithAlpha` (theming rule). |
| 17 | `DividerAlpha = 0xCC` | keep | Drawing alpha. |
| 18 | `LabelAlpha = 0xE6` | keep | Drawing alpha. |
| 19 | `LabelSize = 8f` | keep | Layout pixels. |
| 20 | `LabelPadding = 3` | keep | Layout pixels. |
| 23 | `NeutralTintAlpha = 14` | keep | Drawing alpha. |
| 24 | `NeutralUnderlineAlpha = 0x55` | keep | Drawing alpha. |

### src/Sholto.Interface.MainUI/Controls/Minimap/MinimapMetrics.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 8 | `StripHeight = 64` | keep | Layout pixels. |
| 11 | `LabelRowHeight = 14` | keep | Layout pixels. |
| 14 | `UnderlineHeight = 3` | keep | Layout pixels. |
| 18 | `MinPhraseSpacing = 6` | keep | Layout pixels. |

### src/Sholto.Interface.MainUI/Controls/Modal/ModalShell.axaml.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 17 | `NarrowWidth = 440` | keep | Layout pixels. |
| 18 | `RegularWidth = 600` | keep | Layout pixels. |
| 19 | `WideWidth = 900` | keep | Layout pixels. |
| 20 | `PanelMargin = 20` | keep | Layout pixels. |

### src/Sholto.Interface.MainUI/Controls/DeckSlotPlatter.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 25 | `Size = 30` | keep | Layout pixels. |
| 26 | `Centre = Size / 2` | keep | Maths on layout. |
| 27 | `RingRadius = 13.5` | keep | Layout pixels. |
| 28 | `RingWidth = 2.9` | keep | Layout pixels. |
| 29 | `DiscRadius = 11` | keep | Layout pixels. |
| 30 | `GrooveRadius = 8.5` | keep | Layout pixels. |
| 31 | `LabelRadius = 6` | keep | Layout pixels. |
| 34 | `GhostSize = 64` | keep | Layout pixels. |
| 35 | `GhostScale = GhostSize / Size` | keep | Maths on layout. |
| 36 | `GhostDiscRadius = 14` | keep | Layout pixels. |
| 37 | `GhostLabelRadius = 5.6` | keep | Layout pixels. |
| 38 | `GhostGrooveFirst = 12.6` | keep | Layout pixels. |
| 39 | `GhostGrooveStep = 1.6` | keep | Layout pixels. |
| 40 | `GhostGrooves = 4` | keep | Layout: groove count drawn. |
| 41 | `GhostArcOpacity = 0.45` | keep | Drawing opacity. |

### src/Sholto.Interface.MainUI/Controls/RotaryKnob.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 28 | `Design = 96` | keep | Layout: design-space size. |
| 29 | `CapRadius = 20` | keep | Layout pixels. |
| 30 | `RingRadius = 28` | keep | Layout pixels. |
| 31 | `RingWidth = 4` | keep | Layout pixels. |
| 33 | `GlowWidth = 10` | keep | Layout pixels. |
| 34 | `GlowAlpha = 0x38` | keep | Drawing alpha. |
| 35 | `TickInner = 32` | keep | Layout pixels. |
| 36 | `TickOuter = 36` | keep | Layout pixels. |
| 37 | `TickWidth = 1.5` | keep | Layout pixels. |
| 38 | `LabelRadius = 43` | keep | Layout pixels. |
| 39 | `LabelSize = 9` | keep | Layout pixels. |
| 40 | `PointerInner = 7` | keep | Layout pixels. |
| 41 | `PointerOuter = 17` | keep | Layout pixels. |
| 42 | `PointerWidth = 2.5` | keep | Layout pixels. |
| 44 | `SweepStartDeg = -135` | keep | Maths/layout: knob sweep geometry. |
| 45 | `SweepDeg = 270` | keep | Maths/layout: knob sweep geometry. |
| 47 | `PixelsPerSweep = 200` | move → `DeckViewOptions` (`KnobDragPixelsPerSweep`) | Mouse-drag sensitivity a user might tune. |

### src/Sholto.Interface.MainUI/Controls/CollapseToIcon/CollapseToIconSequenceFactory.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 8 | `InterfaceOrder = 100` | keep | Frame order. |

### src/Sholto.Interface.MainUI/Controls/CollapseToIcon/IconHintPulse.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 19 | `RingRadius = 15` | keep | Layout pixels. |
| 20 | `HaloRadius = 22` | keep | Layout pixels. |
| 21 | `StartScale = 0.7` | keep | Drawing animation geometry. |
| 22 | `EndScale = 2.1` | keep | Drawing animation geometry. |
| 23 | `RingOpacity = 0.85` | keep | Drawing opacity. |
| 24 | `HaloOpacity = 0.9` | keep | Drawing opacity. |
| 25 | `HaloPeak = 0.3` | keep | Drawing animation keyframe. |
| 26 | `HaloAlpha = 0.55` | keep | Drawing alpha. |
| 27 | `OutlineGap = 3` | keep | Layout pixels. |

### src/Sholto.Interface.MainUI/Controls/CollapseToIcon/GsettingsAnimationSetting.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 9 | `TimeoutMs = 1000` | keep | Guard: subprocess hang timeout, failure reads as null. |

### src/Sholto.Interface.MainUI/Controls/CollapseToIcon/IconDockedContainer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 17 | `CollapsedScale = 0.02` | keep | Drawing: near-zero scale of the collapsed state. |

### src/Sholto.Interface.MainUI/Controls/MinimapControl.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 31 | `MarkerHalfWidth = 4` | keep | Layout pixels. |
| 32 | `MarkerHeight = 5.2` | keep | Layout pixels. |

### src/Sholto.Interface.MainUI/Controls/RowSheen.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 35 | `SpotWidth = 150` | keep | Layout pixels. |
| 36 | `CoreAlpha = 0x21` | keep | Drawing alpha. |
| 37 | `FringeAlpha = 0x12` | keep | Drawing alpha. |

### src/Sholto.Interface.MainUI/Controls/WaveformStyles/WaveformEnvelopeBuilder.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 9 | `BakedHeight = 256` | keep | Format: baked bitmap height (same layout fact as `RgbWaveformStrategy.cs:27`). |
| 11 | `BinPx = 3` | keep | Layout pixels: bar width. |
| 12 | `ReleaseCoef = 0.88f` | keep | Maths: envelope coefficient that defines this waveform style. |
| 13 | `Gate = 0.06f` | keep | Maths: style-defining noise gate. |

### src/Sholto.Interface.MainUI/Controls/WaveformStyles/IWaveformBandScaler.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 18 | `DefaultPercentile = 0.97f` | keep | Shared contract: the port and its implementation default to it so they cannot drift. |

### src/Sholto.Interface.MainUI/Controls/WaveformStyles/RgbColourMixer.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 25 | `Sharpness = 2f` | keep | Maths: colour-mix exponent defining the style. |

### src/Sholto.Interface.MainUI/Controls/WaveformStyles/RgbWaveformStrategy.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 27 | `BakedHeight = 256` | keep | Format: baked bitmap height. |
| 29 | `Fill = 0.95f` | keep | Layout: share of lane height. |
| 32 | `HeightGamma = 2f` | keep | Maths: style-defining curve. |
| 34 | `Gate = 0.01f` | keep | Maths: style-defining noise gate. |
| 36 | `BrightnessFloor = 0.35f` | keep | Maths: style-defining brightness curve. |
| 37 | `BrightnessGamma = 0.6f` | keep | Maths: style-defining brightness curve. |

### src/Sholto.Interface.MainUI/Controls/WaveformStyles/DemoWaveformFactory.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 12 | `Seed = 20261004` | keep | Format: fixed seed so the demo waveform is reproducible. |
| 13 | `Beats = 256` | keep | Demo data shape. |
| 14 | `SlotsPerBeat = 8` | keep | Demo data shape. |
| 15 | `DemoBpm = 128` | keep | Demo data shape. |
| 16 | `DemoSampleRate = 48000` | keep | Format fact; MainUI may not reference `EngineAudio` (Interface → Data only). |
| 29 | `samplesPerPeak = 1024` | keep | Format fact mirroring `WaveformDefaults.SamplesPerPeak`, same layering reason. |
| 127 | `radius = 2` | keep | Maths: smoothing kernel radius of the demo data. |

### src/Sholto.Interface.MainUI/Controls/FitShimmerColumn.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 44 | `RowPitch = 42` | keep | Layout pixels. |
| 45 | `BarWidth = 6` | keep | Layout pixels. |
| 46 | `BarHeight = 30` | keep | Layout pixels. |
| 47 | `NeutralOpacity = 0.35` | keep | Drawing opacity. |
| 50 | `StaggerPerRow = 0.055` | keep | Drawing animation timing share. |
| 53 | `TravelShare = 0.6` | keep | Drawing animation timing share. |

### src/Sholto.Interface.MainUI/Controls/WaveformBlitOperation.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 143 | `bandH = 6f` | keep | Layout pixels. |
| 255 | `dedupeTol = 0.001` | keep | Guard: float tolerance. |

### src/Sholto.Interface.MainUI/Controls/ThemeMiniatureWaveform.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 14 | `Columns = 44` | keep | Layout. |
| 15 | `Gap = 1` | keep | Layout pixels. |

### src/Sholto.Interface.MainUI/Controls/WaveformPreviewControl.cs

| Line | Const | Verdict | Reason |
|---|---|---|---|
| 15 | `PlayheadFraction = 0.45` | keep | Layout: playhead position in the preview. |
| 16 | `PlayheadWidth = 2` | keep | Layout pixels. |

## Grep hits that are not declarations

| File:line | Verdict | Reason |
|---|---|---|
| `src/Sholto.App.Analysis/EngineAudio.cs:3` | n/a | Doc comment mentioning `const`. |
| `src/Sholto.App.ExternalTools/ExternalToolCatalog.cs:11` | n/a | Doc comment mentioning `const`. |
| `src/Sholto.App.ExternalTools/ExternalToolOptions.cs:39` | n/a | Doc comment mentioning `const`. |

## Follow-ups (move verdicts not done here)

Each is its own job: add the property with today's value as default, read it once in the
constructor, register the options class in `SholtoOptions` (`Sholto.Host`) if new, and add a
defaults-pinning test.

| Const(s) | Options class to join |
|---|---|
| `DeckPlayPhase.FlashHalfPeriodMs`, `NearEndPosition` | `DeckPlayOptions` (new, `Sholto.App`) |
| `VocalRegionAnalyzer.PresenceThreshold`, `MinSpanSec`, `MergeGapSec` | `VocalRegionOptions` (new, `Sholto.App.Analysis`) |
| `PipeWireRouter` `attempts`, `delayMs` | `AudioRoutingOptions` (new, `Sholto.App.Audio`) |
| `SqlitePragmaInterceptor.BusyTimeoutMs` | `DatabaseOptions` (new, `Sholto.App.Storage`) |
| `EchoEffect.Feedback`, `Wet`, `CapacitySeconds`; `BeatRepeatEffect.CapacitySeconds` | `EffectOptions` (new, `Sholto.App.Audio`), read once in the constructor, never on the audio thread |
| `DjFilter.LpMinHz`, `LpMaxHz`, `HpMinHz`, `HpMaxHz` | `FilterOptions` (new, `Sholto.App.Audio`), read once in the constructor |
| `StemFadeIn.FadeFrames`, `ScratchDataProvider.FadeFrames` | `DeclickOptions` (new, `Sholto.App.Audio`), read once in the constructor |
| `DiscBloom.MaxStepSeconds`, `WaveformPreviewScroll.MaxStepSeconds`, `DeckSlotViewModel.LongestFrameSeconds` | `DeckViewOptions` (existing, `Sholto.Interface.MainUI`) as one `MaxFrameStepSeconds` |
| `RotaryKnob.PixelsPerSweep` | `DeckViewOptions` (existing) as `KnobDragPixelsPerSweep` |
| `TagEditorViewModel.maxSuggestions` | `GlanceViewOptions` (existing) as `TagSuggestionLimit` |
| `SearchOverlayResultsSync.FlashMs` | `GlanceViewOptions` (existing) as `JoinFlashMs` |
