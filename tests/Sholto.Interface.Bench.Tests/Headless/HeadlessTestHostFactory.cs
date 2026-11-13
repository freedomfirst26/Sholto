using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.Data;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Rendering;

namespace Sholto.Interface.Bench.Tests.Headless;

/// <summary>Builds the <see cref="IHeadlessHostFactory"/> the headless tests run against: real WAV/MP3
/// decoding, an immediate app thread, no stem or file analysis.</summary>
public sealed class HeadlessTestHostFactory
{
    public IHeadlessHostFactory Create()
    {
        var naudio = new NAudioDecoding();
        var decoder = new AudioFileDecoder([new Mp3DecodeStrategy(naudio), new WavDecodeStrategy(naudio)]);
        var appThread = new ImmediateAppThread();
        var deckFactory = new BenchDeckFactory(
            decoder,
            new NoOpStemAnalysisStep(),
            new AnalysisProvider((_, _) => throw new NotSupportedException("not used")),
            Options.Create(new WaveformBandOptions()),
            appThread, new RecordingLogFactory());
        return new HeadlessHostFactory(new BenchDeck(deckFactory), deckFactory, decoder, new DeckAdvance(deckFactory), appThread, new EqualPowerCrossfade(), new RecordingLogFactory());
    }
}
