using Sholto.App.Analysis.Stems;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.TestSupport;

/// <summary>Builds a deck through the real <see cref="DeckFactory"/>, wired as
/// SholtoStackFactory wires it, except that every collaborator that would reach a
/// subprocess, the database or a basic-analysis run is its inert stand-in
/// (<see cref="NullExternalTool"/>, the Null* stores, an analysis provider that throws).
/// The tests using it never load a track, so none of those is reached.</summary>
internal sealed class TestDeckFactory : IDeckFactory
{
    public IDeckPorts Create()
    {
        var decoder = new AudioFileDecoder([]);
        var reporter = new AnalysisReporter(Array.Empty<string>());
        var peaks = new WaveformPeakAnalyzer(
            new WaveformBandSplitterFactory(new BiquadFactory(), Options.Create(new WaveformBandOptions())),
            new WaveformPeaksFactory());
        var stemStage = new StemAnalysisStage(
            new DemucsStemAnalysisStep(new NullExternalTool(), _ => Path.GetTempPath(), new FixedStemDevice(StemDevice.Cpu)),
            new StemDecoder(decoder), peaks, new VocalRegionAnalyzer(), reporter,
            new FixedStemDevice(StemDevice.Cpu), new SemaphoreStemGate());
        var decks = new DeckFactory(
            decoder, stemStage, reporter,
            new AnalysisProvider((_, _) =>
                throw new NotImplementedException("TestDeckFactory decks never run basic analysis.")),
            new NullKeyAnalysisStore(), new NullGridAdjustmentStore(), new KeyAnalyzer(), new BeatgridFactory(),
            new DeckEffectFactory(new TempoMath()).Chain, new PlaybackProviderFactory(),
            new ImmediateAppThread());
        return decks.Create();
    }
}
