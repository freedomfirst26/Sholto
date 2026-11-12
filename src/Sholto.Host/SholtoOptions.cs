using Sholto.App.Audio;
using Sholto.App;
using Sholto.App.Decks;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.Controller.Mappings;
using Sholto.Interface.Controller.WinMm;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

using Sholto.Interface.MainUI;
using Sholto.Data;

namespace Sholto.Host;

/// <summary>One place that holds the app's configured options objects, so every
/// composition root — the real <c>App</c> and <c>tools/Sholto.Interface.Bench</c>'s
/// composers — constructs the same defaults instead of each declaring its own
/// <c>new XOptions()</c>.
///
/// <para>Bench already references <c>Sholto.Interface.MainUI</c>, which already references
/// <c>Sholto.Interface.Controller.Mappings</c>, so all options types here are visible
/// from Bench today: no new project is needed to construct one there. The one
/// condition that would change that: if <c>Sholto.App.Audio</c> or <c>Sholto.Interface.Controller</c> ever needed to READ configured
/// values, they could not reference <c>Sholto.Interface.MainUI</c> without inverting the
/// onion. Not true today.</para></summary>
public sealed class SholtoOptions
{
    public IOptions<FeatureOptions> Feature { get; }
    public IOptions<ControllerMappingsOptions> Controllers { get; }
    public IOptions<ScratchOptions> Scratch { get; }
    public IOptions<MagnetismOptions> Magnetism { get; }
    public IOptions<GlanceOptions> Glance { get; }
    public IOptions<WaveformBandOptions> WaveformBands { get; }
    public IOptions<KeyAnalysisOptions> KeyAnalysis { get; }
    public IOptions<RenderingOptions> Rendering { get; }
    public IOptions<LoadOptions> Load { get; }
    public IOptions<HotCueOptions> HotCue { get; }
    public IOptions<GlanceViewOptions> GlanceView { get; }
    public IOptions<DeckViewOptions> DeckView { get; }
    public IOptions<WaveformStyleOptions> WaveformStyle { get; }
    public IOptions<StemSeparationOptions> StemSeparation { get; }
    public IOptions<LoopOptions> Loop { get; }
    public IOptions<AudioTimingOptions> AudioTiming { get; }
    public IOptions<AnalysisFolderOptions> AnalysisFolder { get; }
    public IOptions<FoldersPanelOptions> FoldersPanel { get; }
    public IOptions<WinMmMidiOptions> WinMmMidi { get; }

    /// <summary>The app's configured options, at their defaults. Each composition
    /// root (Program, Bench's composers) builds one instance and hands it down. An
    /// environment override is reported through <paramref name="logs"/>.</summary>
    public SholtoOptions(ILogFactory logs)
    {
        Feature = Options.Create(new FeatureOptions());
        Controllers = Options.Create(new ControllerMappingsOptions());
        // The one place the SHOLTO_SCRATCH_LOG and SHOLTO_AUDIO_TIMING environment switches are read.
        Scratch = Options.Create(new ScratchOptionsFactory(Environment.GetEnvironmentVariable).Create());
        Magnetism = Options.Create(new MagnetismOptions());
        Glance = Options.Create(new GlanceOptions());
        WaveformBands = Options.Create(new WaveformBandOptions());
        KeyAnalysis = Options.Create(new KeyAnalysisOptions());
        Rendering = Options.Create(new RenderingOptions());
        Load = Options.Create(new LoadOptions());
        HotCue = Options.Create(new HotCueOptions());
        // One tempo for "no analysed BPM": the platter's and the LOAD TO slot's agree.
        GlanceView = Options.Create(new GlanceViewOptions { FallbackBpm = Scratch.Value.FallbackBpm });
        DeckView = Options.Create(new DeckViewOptions());
        WaveformStyle = Options.Create(new WaveformStyleOptions());
        StemSeparation = Options.Create(new StemSeparationOptions());
        // The one place the SHOLTO_LOOP_* environment overrides are read.
        Loop = Options.Create(new LoopOptionsFactory(Environment.GetEnvironmentVariable, logs).Create());
        AudioTiming = Options.Create(new AudioTimingOptionsFactory(Environment.GetEnvironmentVariable).Create());
        AnalysisFolder = Options.Create(new AnalysisFolderOptions());
        FoldersPanel = Options.Create(new FoldersPanelOptions());
        WinMmMidi = Options.Create(new WinMmMidiOptions());
    }
}
